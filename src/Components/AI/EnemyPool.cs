using System.Collections.Generic;
using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.AI;

/// <summary>
/// Estoque de inimigos pré-instanciados, reaproveitados em vez de destruídos.
/// </summary>
/// <remarks>
/// Autoload, prewarming no próprio <c>_Ready</c> -- "o estoque é preparado
/// antes da partida começar" (ticket 25) já vale trivialmente assim, mesmo
/// sem nenhum sistema de onda ainda (ticket 27): igual a
/// <c>ProjectilePool</c>/<c>DamageNumberPool</c>, que já prewarmam no boot.
///
/// **Nunca chama <c>Instantiate</c>/<c>QueueFree</c> fora do prewarm** (ADR-009
/// em docs/plans/riscos-e-decisoes.md): um inimigo "morto" só é desativado
/// (escondido, sem colisão, `ProcessMode.Disabled`) e volta para a pilha
/// livre da própria espécie. Crescer além do prewarm em <see cref="Acquire"/>
/// é o único caminho que ainda instancia em runtime, e só como último
/// recurso -- avisa alto, não falha silenciosamente.
///
/// Ao contrário de <c>ProjectilePool</c> (dados puros, sem nó por projétil),
/// um inimigo é uma árvore inteira de componentes que o CHAMADOR precisa
/// referenciar de volta (contagem de onda, futuro), então <see cref="Acquire"/>
/// é uma chamada direta que devolve o próprio <see cref="CharacterController"/>
/// -- não um evento no <see cref="Contenda.Core.GameEvents"/> como
/// <c>ProjectileFireRequested</c>. `Release`, por outro lado, É acionado pelo
/// próprio <see cref="EnemyBrain"/> ao fim da morte: como o barramento global
/// só transporta dados por valor, nunca nós (regra explícita de
/// <c>GameEvents</c>), o <see cref="EnemyBrain"/> guarda uma referência direta
/// a ESTE pool (entregue por <see cref="Prewarm"/>/<see cref="Acquire"/> via
/// <c>CharacterContext.EnemyBrain</c>), sem string-path nenhum.
/// </remarks>
public sealed partial class EnemyPool : Node
{
    /// <summary>Cena do grunt a prewarmar no boot.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string GruntScenePath { get; set; } = "res://scenes/characters/EnemyGrunt.tscn";

    /// <summary>Definição de comportamento do grunt a prewarmar no boot.</summary>
    [Export(PropertyHint.File, "*.tres")]
    public string GruntDefinitionPath { get; set; } = "res://data/enemies/grunt.tres";

    /// <summary>Quantos grunts pré-alocar. Ver dimensionamento em spec 09 §8.</summary>
    [Export(PropertyHint.Range, "1,200,1")]
    public int GruntPoolSize { get; set; } = 60;

    /// <summary>As camadas de colisão originais de uma instância, capturadas antes da primeira desativação.</summary>
    private readonly record struct ColisaoOriginal(uint Camada, uint Mascara);

    /// <summary>Uma espécie de inimigo: sua cena, definição e as instâncias já criadas.</summary>
    private sealed class Especie
    {
        public required PackedScene Cena { get; init; }
        public readonly Stack<CharacterController> Livres = new();
        public readonly Dictionary<CharacterController, ColisaoOriginal> ColisaoOriginalPorInstancia = new();
    }

    private readonly Dictionary<EnemyDefinition, Especie> _especies = new();
    private readonly Dictionary<CharacterController, Especie> _especiePorInstancia = new();
    private readonly HashSet<CharacterController> _ativos = new();

    /// <summary>Quantos inimigos estão fora da pilha livre agora (vivos ou ainda "morrendo"). Para o probe/depuração.</summary>
    public int ActiveCount => _ativos.Count;

    /// <summary>
    /// Preenche <paramref name="destino"/> (limpo primeiro) com a posição de
    /// cada inimigo ativo agora, de qualquer espécie, exceto
    /// <paramref name="excluir"/>.
    /// </summary>
    /// <remarks>
    /// Usado pela força de separação (ticket 23, spec 09 §4): cada inimigo em
    /// <c>Chase</c> precisa da posição dos OUTROS para não se amontoar, e o
    /// pool já é a única fonte de verdade de "quem está ativo agora" -- sem
    /// isto, cada <c>EnemyBrain</c> teria que descobrir os vizinhos por conta
    /// própria (um grupo `Node`, uma query de física), duplicando o que o
    /// pool já sabe.
    ///
    /// Recebe o buffer do CHAMADOR (não devolve uma lista nova) e itera
    /// <c>_ativos</c> pelo próprio tipo concreto (`HashSet&lt;T&gt;`), não por
    /// `IEnumerable&lt;T&gt;`/`IReadOnlyCollection&lt;T&gt;` -- convenções §5 e spec 15
    /// §3 proíbem alocação por quadro no hot path, e isto roda a cada
    /// <c>EnemyBrain.Poll</c> em `Chase`: uma lista nova por chamada E o
    /// enumerador de `HashSet&lt;T&gt;` boxed (só acontece quando iterado por trás
    /// de uma interface) seriam duas alocações por quadro, por inimigo.
    /// <paramref name="excluir"/> existe porque o próprio chamador está em
    /// <c>_ativos</c> -- sem excluir a si mesmo, todo inimigo se
    /// "separaria" da própria posição, distância zero.
    /// </remarks>
    public void ObterPosicoesAtivas(CharacterController? excluir, List<Vector3> destino)
    {
        destino.Clear();

        foreach (var ativo in _ativos)
        {
            if (!ReferenceEquals(ativo, excluir) && GodotObject.IsInstanceValid(ativo))
                destino.Add(ativo.GlobalPosition);
        }
    }

    /// <summary>
    /// Enfileira a morte de cada inimigo que pediu movimento mas ficou sem
    /// avançar pelo tempo limite. Retorna quantos foram marcados.
    /// </summary>
    public int KillStuckEnemies(float timeoutSeconds)
    {
        var marcados = 0;

        foreach (var ativo in _ativos)
        {
            if (!GodotObject.IsInstanceValid(ativo)
                || ativo.Context is not { } contexto
                || contexto.Health is not { IsAlive: true, IsInvulnerable: false } vida
                || contexto.EnemyBrain is not { } cerebro
                || !cerebro.TryMarkStuckForRemoval(timeoutSeconds))
                continue;

            vida.Kill("stuck-enemy-fallback");
            marcados++;
        }

        return marcados;
    }

    /// <summary>
    /// Se o prewarm padrão (grunt, no boot) já terminou. Falso por um ou dois
    /// quadros logo no início -- ver o remark de <see cref="_Ready"/>.
    /// </summary>
    public bool IsReady { get; private set; }

    public override void _Ready()
    {
        // Adiado: um `CharacterBody3D` instanciado no PRÓPRIO `_Ready` de um
        // autoload -- antes da árvore ter rodado seu primeiro `_PhysicsProcess`
        // -- ainda não tem espaço físico atribuído pelo servidor, e um
        // `MoveAndSlide` involuntário nesse instante (mesmo com o processo já
        // desligado por `Desativar`, que só o some da fila do QUADRO SEGUINTE
        // em diante) falha com "body->get_space() is null". `ProjectilePool`/
        // `DamageNumberPool` nunca bateram nisto por só criarem
        // `MeshInstance3D` -- sem corpo físico, sem `MoveAndSlide` nenhum.
        // Mesmo remédio que `WorldHealthBar.Atualizar` já usa para uma
        // defasagem parecida.
        CallDeferred(nameof(PrewarmGruntPadrao));
    }

    private void PrewarmGruntPadrao()
    {
        var cena = GD.Load<PackedScene>(GruntScenePath);
        var definicao = GD.Load<EnemyDefinition>(GruntDefinitionPath);

        if (cena is null || definicao is null)
        {
            GD.PushError($"{Name}: não consegui carregar {GruntScenePath} ou {GruntDefinitionPath}.");
            return;
        }

        Prewarm(cena, definicao, GruntPoolSize);
        IsReady = true;
        GD.Print($"[boot] EnemyPool pronto ({GruntPoolSize} grunts pré-alocados)");
    }

    /// <summary>Se esta espécie já foi prewarmada. Ver <see cref="Prewarm"/>.</summary>
    public bool IsPrewarmed(EnemyDefinition definicao) => _especies.ContainsKey(definicao);

    /// <summary>Cria e desativa <paramref name="quantidade"/> instâncias de uma espécie, prontas para <see cref="Acquire"/>.</summary>
    public void Prewarm(PackedScene cena, EnemyDefinition definicao, int quantidade)
    {
        if (!_especies.TryGetValue(definicao, out var especie))
        {
            especie = new Especie { Cena = cena };
            _especies[definicao] = especie;
        }

        for (var i = 0; i < quantidade; i++)
        {
            var instancia = CriarInstancia(cena, definicao, especie);
            especie.Livres.Push(instancia);
        }
    }

    /// <summary>
    /// Tira um inimigo do estoque, já resetado e reposicionado. Cresce além
    /// do prewarm (com aviso) se a pilha livre da espécie estiver vazia.
    /// </summary>
    public CharacterController? Acquire(EnemyDefinition definicao, Vector3 posicao)
    {
        if (!_especies.TryGetValue(definicao, out var especie))
        {
            GD.PushError($"{Name}: Acquire chamado para uma EnemyDefinition nunca prewarmada.");
            return null;
        }

        CharacterController instancia;
        if (especie.Livres.Count > 0)
        {
            instancia = especie.Livres.Pop();
        }
        else
        {
            // Instanciar aqui é o preço de não deixar a onda travada por um
            // dimensionamento curto -- ADR-009 aceita o pool esgotar-se como
            // caminho raro, não como caminho normal (daí o aviso, não um erro
            // silencioso).
            GD.PushWarning($"{Name}: estoque esgotado, instanciando além do prewarm.");
            instancia = CriarInstancia(especie.Cena, definicao, especie);
        }

        instancia.ResetForSpawn();
        instancia.GlobalPosition = posicao;

        // Ligar `ProcessMode`/colisão de volta é adiado por um quadro, não
        // síncrono aqui: reativar NO MESMO quadro em que acabaram de ser
        // desligados (na morte ou no `Desativar` do prewarm) faz o corpo
        // tentar um `MoveAndSlide` antes do servidor físico reanexar o
        // espaço dele -- "body->get_space() is null". Mesma defasagem do
        // prewarm em <see cref="_Ready"/>, só que na direção contrária:
        // reposicionar e resetar dados não mexem com física nenhuma, então
        // só a reativação física precisa esperar.
        _especiePorInstancia[instancia] = especie;
        _ativos.Add(instancia);
        CallDeferred(nameof(ReativarAdiado), instancia);

        return instancia;
    }

    private void ReativarAdiado(CharacterController inimigo)
    {
        // Pode já ter voltado ao pool de novo entre o pedido e a execução
        // adiada, num teste de ciclo bem apertado -- neste caso, `Reativar`
        // ligaria de volta algo que `Release` acabou de desligar de
        // propósito. `_ativos` é a fonte da verdade de "ainda devia estar
        // ligado" no momento em que isto roda de verdade.
        if (_especiePorInstancia.TryGetValue(inimigo, out var especie) && _ativos.Contains(inimigo))
            Reativar(inimigo, especie);
    }

    /// <summary>Devolve um inimigo ao estoque da própria espécie. Chamado pelo <see cref="EnemyBrain"/> ao fim da morte.</summary>
    public void Release(CharacterController inimigo)
    {
        if (!_especiePorInstancia.TryGetValue(inimigo, out var especie))
        {
            GD.PushError($"{Name}: Release chamado para um inimigo que este pool não reconhece.");
            return;
        }

        Desativar(inimigo);
        especie.Livres.Push(inimigo);
        _ativos.Remove(inimigo);
    }

    private CharacterController CriarInstancia(PackedScene cena, EnemyDefinition definicao, Especie especie)
    {
        var instancia = (CharacterController)cena.Instantiate();
        AddChild(instancia);

        // As camadas de colisão vêm prontas da própria cena (`.tscn`), antes
        // de qualquer morte zerá-las -- capturar aqui, uma vez só, é o que
        // permite restaurar exatamente o valor certo em toda `Acquire`
        // futura, mesmo depois de dezenas de ciclos de morte/reciclagem.
        especie.ColisaoOriginalPorInstancia[instancia] = new ColisaoOriginal(instancia.CollisionLayer, instancia.CollisionMask);

        if (instancia.Context?.EnemyBrain is { } cerebro)
        {
            cerebro.Pool = this;

            // A cena traz sua PRÓPRIA `EnemyBrain.Definition` (ex.:
            // `EnemyGrunt.tscn` já vem com `grunt.tres`) -- mas quem chama
            // `Prewarm`/`Acquire` decide qual definição esta espécie É,
            // e as duas podem divergir de propósito (a mesma cena servindo
            // de base para uma variante elite/chefe via um `.tres` diferente,
            // ticket 26, sem precisar de uma cena nova por variante). A do
            // parâmetro sempre vence. `AddChild`, acima, já rodou `Bind` e
            // `Configure` com a definição ANTIGA (a da cena) -- por isso
            // `Acquire` sempre chama `ResetForSpawn` de novo depois de criar,
            // que reconstrói tudo (máquina de estado, tingimento de elite,
            // anúncio de chefe) a partir da definição já trocada aqui.
            cerebro.Definition = definicao;
        }
        else
        {
            GD.PushError($"{Name}: {instancia.Name} não tem EnemyBrain -- só inimigos de verdade entram neste pool.");
        }

        Desativar(instancia);

        return instancia;
    }

    private static void Desativar(CharacterController inimigo)
    {
        inimigo.Visible = false;
        inimigo.ProcessMode = ProcessModeEnum.Disabled;
        inimigo.CollisionLayer = 0;
        inimigo.CollisionMask = 0;
    }

    private static void Reativar(CharacterController inimigo, Especie especie)
    {
        var colisao = especie.ColisaoOriginalPorInstancia[inimigo];

        inimigo.Visible = true;
        inimigo.ProcessMode = ProcessModeEnum.Inherit;
        inimigo.CollisionLayer = colisao.Camada;
        inimigo.CollisionMask = colisao.Mascara;
    }
}
