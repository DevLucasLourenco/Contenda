using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Health;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica a morte de um inimigo e o ciclo completo de reciclagem pelo
/// <see cref="EnemyPool"/> na árvore de nós real, sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem <c>EnemyStateMachine</c> isolada -- as
/// transições de e para <see cref="EnemyState.Death"/> em si. O que eles não
/// alcançam é a fiação real: se morrer de verdade desliga a colisão e
/// mantém o corpo visível até <c>DeathDuration</c>, se devolve ao pool
/// sozinho (sem ninguém chamar <c>Release</c> à mão), e se <c>Acquire</c>
/// depois disso devolve um inimigo GENUINAMENTE idêntico a um recém-criado
/// -- o contrato de reciclagem que o ticket 25 e a spec 09 §8 exigem, e cujo
/// jeito mais confiável de furar é justamente o que só aparece depois de
/// muitos ciclos.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/EnemyPoolProbe.tscn
/// </code>
/// </remarks>
public sealed partial class EnemyPoolProbe : Node
{
    /// <summary>Cena com o chão/navmesh.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    /// <summary>A mesma definição que o <see cref="EnemyPool"/> prewarma por padrão -- precisa ser o MESMO `.tres`, para bater por referência.</summary>
    [Export(PropertyHint.File, "*.tres")]
    public string GruntDefinitionPath { get; set; } = "res://data/enemies/grunt.tres";

    private const int CiclosDeStress = 500;

    /// <summary>
    /// Um canto do chão de 60×60 m da arena (ver <c>Shape_chao</c> em
    /// Arena.tscn), bem longe do "Jogador" de verdade (nasce em (0,0,0),
    /// ticket 22): perto dele, a IA detectaria e sairia de <c>Idle</c> por
    /// conta própria a qualquer momento -- correto para o jogo, mas exatamente
    /// a mesma corrida já resolvida no ticket 24 (checar "ainda Idle" vira
    /// instável perto de um alvo visível). ~35 m de (0,0,0), além dos 30 m de
    /// <c>LoseTargetRadius</c> -- mas ainda EM CIMA do chão, não flutuando no
    /// vazio: testar reciclagem precisa de um grunt que realmente assenta.
    /// </summary>
    // RuaNorteOeste (Arena.tscn, ticket 21): chão real, plano, x ∈ [-16, -7],
    // z ∈ [-19, -8] -- (25, 25) caía dentro do prédio da quina SE da arena
    // urbana, e um grunt reaquirido lá nunca assentava (chão sólido demais
    // perto, IsGrounded nunca virava true, "impulso de vida anterior" nunca
    // zerava).
    private static readonly Vector3 Longe = new(-14f, 0.1f, -17f);

    private readonly List<string> _falhas = [];
    private EnemyPool? _pool;
    private EnemyDefinition? _definicao;

    private CharacterController? _grunt;
    private CharacterController? _instanciaDeStress;
    private readonly List<CharacterController> _inimigosDaRajada = [];
    private int _ativosAntesDoTeste;
    private int _cicloAtual;
    private int _quadrosDoCiclo;
    private int _nosAntesDosCiclos;
    private bool _sujandoNesteCiclo;

    private enum Fase { CicloNatural, ReaquisicaoLimpa, ReciclagemDeStress, RajadaDeOnda }
    private Fase _fase = Fase.CicloNatural;
    private int _quadroDaFase;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[pool-inimigo] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());

        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");
        _definicao = GD.Load<EnemyDefinition>(GruntDefinitionPath);

        if (_pool is null || _definicao is null)
        {
            GD.PrintErr("[pool-inimigo] FALHA: EnemyPool ou EnemyDefinition não resolveram.");
            GetTree().Quit(1);
            return;
        }

        _ativosAntesDoTeste = _pool.ActiveCount;

        GD.Print("[pool-inimigo] pool pronto");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pool is null || _definicao is null)
            return;

        // O prewarm padrão do EnemyPool é adiado (CallDeferred, ver o remark
        // do próprio pool) para não instanciar CharacterBody3D antes da
        // árvore ter espaço físico -- não conta quadro nenhum até ele
        // terminar, em vez de chutar quantos quadros isso leva.
        if (!_pool.IsReady)
            return;

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.CicloNatural: TickCicloNatural(); break;
            case Fase.ReaquisicaoLimpa: TickReaquisicaoLimpa(); break;
            case Fase.ReciclagemDeStress: TickReciclagemDeStress(); break;
            case Fase.RajadaDeOnda: TickRajadaDeOnda(); break;
            default: break;
        }
    }

    private void TickCicloNatural()
    {
        if (_quadroDaFase == 1)
        {
            _grunt = _pool!.Acquire(_definicao!, Longe);
            if (_grunt is null)
            {
                Verificar(false, "Acquire devolveu null no primeiro pedido -- prewarm falhou.");
                Concluir();
                return;
            }

            Verificar(_pool.ActiveCount == _ativosAntesDoTeste + 1, "ActiveCount deveria subir 1 ao adquirir.");
            return;
        }

        if (_quadroDaFase == 5)
        {
            // Suja o estado ANTES de matar: se a reciclagem não limpar isto,
            // o próximo Acquire vai herdar dano/atordoamento/impulso de uma
            // vida que já acabou.
            _grunt!.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: 15f, Type: DamageType.Physical, HitPoint: _grunt.GlobalPosition, Direction: Vector3.Forward,
                Knockback: 0f, SourceId: 0UL, SourceTag: "enemy_pool_probe", IsCritical: false));
            _grunt.Context.Movement!.ApplyKnockback(Vector3.Forward * 3f);
            return;
        }

        if (_quadroDaFase == 8)
        {
            Verificar(_grunt!.Context!.EnemyBrain!.Estado == EnemyState.Staggered,
                "o golpe de sujar deveria ter atordoado antes de matar -- senão o teste não prova nada.");

            _grunt.Context.Health!.Kill("enemy_pool_probe");
            return;
        }

        if (_quadroDaFase == 10)
        {
            Verificar(_grunt!.Context!.EnemyBrain!.Estado == EnemyState.Death,
                $"morrer deveria entrar em Death; está {_grunt.Context.EnemyBrain.Estado}");
            Verificar(_grunt.CollisionLayer == 0 && _grunt.CollisionMask == 0,
                "a colisão deveria estar desligada assim que morre, não só ao voltar ao pool.");
            Verificar(_grunt.Visible, "o corpo deveria continuar visível durante a janela de \"animação\" de morte.");
            Verificar(_pool!.ActiveCount == _ativosAntesDoTeste + 1,
                "ainda dentro da janela de morte, o inimigo continua contando como ativo (não voltou ao pool ainda).");
            return;
        }

        // DeathDuration do grunt.tres é 1.2 s (72 quadros a 60 Hz); folga
        // generosa para não empatar com o exato limite.
        if (_quadroDaFase < 100)
            return;

        Verificar(!_grunt!.Visible, "depois de DeathDuration, o corpo deveria ter sumido.");
        Verificar(_grunt.ProcessMode == Node.ProcessModeEnum.Disabled,
            "depois de DeathDuration, o processamento deveria estar desligado.");
        Verificar(_pool!.ActiveCount == _ativosAntesDoTeste,
            "depois de DeathDuration, o inimigo deveria ter voltado sozinho ao pool -- ninguém chamou Release à mão.");

        AvancarFase(Fase.ReaquisicaoLimpa);
    }

    private void TickReaquisicaoLimpa()
    {
        if (_quadroDaFase == 1)
        {
            _grunt = _pool!.Acquire(_definicao!, Longe + new Vector3(2f, 0f, 0f));
            return;
        }

        // Alguns quadros de física para assentar no chão de verdade, não só
        // no ESTADO "pronto".
        if (_quadroDaFase < 8)
            return;

        VerificarInstanciaLimpa(_grunt!, "reaquisição");

        _pool!.Release(_grunt!);
        _grunt = null;
        Verificar(_pool.ActiveCount == _ativosAntesDoTeste,
            "a preparação do stress deveria devolver o grunt ao pool antes dos 500 ciclos.");

        AvancarFase(Fase.ReciclagemDeStress);
    }

    private void TickReciclagemDeStress()
    {
        // Mede reciclagem real pelo pool, sem esperar a animação de morte: cada
        // repetição suja o estado, devolve o nó e o readquire como faria uma
        // onda. A cena já cobre a janela natural de morte em TickCicloNatural.
        if (_quadroDaFase == 1)
        {
            _cicloAtual = 0;
            _quadrosDoCiclo = 0;
            _nosAntesDosCiclos = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            _sujandoNesteCiclo = false;
        }

        if (_grunt is null)
        {
            var adquirido = _pool!.Acquire(_definicao!, Longe);
            if (adquirido is null)
            {
                Verificar(false, $"ciclo {_cicloAtual + 1}: Acquire deveria reutilizar uma instância do pool.");
                Concluir();
                return;
            }

            _grunt = adquirido;
            _quadrosDoCiclo = 0;
            _sujandoNesteCiclo = false;
            return;
        }

        _quadrosDoCiclo++;
        if (_quadrosDoCiclo == 8)
        {
            Verificar(_pool!.ActiveCount == _ativosAntesDoTeste + 1,
                $"ciclo {_cicloAtual + 1}: Acquire deveria marcar exatamente um inimigo ativo.");
            VerificarInstanciaLimpa(_grunt, $"ciclo {_cicloAtual + 1}/{CiclosDeStress}");
            if (_instanciaDeStress is null)
                _instanciaDeStress = _grunt;
            else
                Verificar(ReferenceEquals(_instanciaDeStress, _grunt),
                    $"ciclo {_cicloAtual + 1}: o pool deveria reutilizar a mesma instância, sem criar nós novos.");
            return;
        }

        if (_quadrosDoCiclo < 8)
            return;

        if (!_sujandoNesteCiclo)
        {
            _grunt!.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: 20f, Type: DamageType.Physical, HitPoint: _grunt.GlobalPosition, Direction: Vector3.Forward,
                Knockback: 0f, SourceId: 0UL, SourceTag: "enemy_pool_probe", IsCritical: false));
            _grunt.Context.Movement!.ApplyKnockback(Vector3.Up * 2f);
            _sujandoNesteCiclo = true;
            return; // um quadro de folga para o dano enfileirado resolver de verdade
        }

        // Confirma que sujou de verdade ANTES de resetar -- senão isto
        // provaria só que resetar um estado já limpo continua limpo.
        Verificar(_grunt!.Context!.Health!.Percent < 0.999f,
            $"ciclo {_cicloAtual + 1}: o dano de sujar deveria ter aplicado de verdade antes do reset.");

        _pool!.Release(_grunt);
        _grunt = null;
        Verificar(_pool.ActiveCount == _ativosAntesDoTeste,
            $"ciclo {_cicloAtual + 1}: Release deveria devolver o pool à contagem inicial.");

        _cicloAtual++;

        if (_cicloAtual >= CiclosDeStress)
        {
            var nosDepoisDosCiclos = (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            Verificar(nosDepoisDosCiclos == _nosAntesDosCiclos,
                $"500 reciclagens não deveriam acumular nós; antes {_nosAntesDosCiclos}, depois {nosDepoisDosCiclos}.");
            AvancarFase(Fase.RajadaDeOnda);
        }
    }

    private void TickRajadaDeOnda()
    {
        if (_quadroDaFase == 1)
        {
            // Pede exatamente o que resta do estoque prewarmado (60 grunts
            // menos o que as fases anteriores ainda seguram ativo), não um
            // número arbitrário menor -- é a única forma desta fase provar
            // que uma onda do TAMANHO DO PRÓPRIO DIMENSIONAMENTO (spec 09 §8)
            // sai inteira do estoque, sem cair no caminho de `CriarInstancia`
            // além do prewarm (esse sim instancia em runtime, e é exatamente
            // o engasgo que o ticket pede para evitar).
            var tamanhoDaRajada = _pool!.GruntPoolSize - _pool.ActiveCount;

            var adquiridos = 0;
            for (var i = 0; i < tamanhoDaRajada; i++)
            {
                var posicao = Longe + new Vector3(i * 1.5f, 0f, 20f);
                if (_pool.Acquire(_definicao!, posicao) is { } inimigo)
                {
                    adquiridos++;
                    _inimigosDaRajada.Add(inimigo);
                }
            }

            // Aqui não há como medir frame time de verdade (isto é um probe,
            // não um profiler), mas provar que o estoque inteiro sai num
            // quadro só, sem ninguém avisar de estoque esgotado, é a garantia
            // que existe PARA evitar o engasgo em primeiro lugar.
            Verificar(adquiridos == tamanhoDaRajada,
                $"a rajada deveria adquirir os {tamanhoDaRajada} grunts restantes do estoque; só {adquiridos} vieram sem erro.");
            Verificar(_pool.ActiveCount == _pool.GruntPoolSize,
                $"a rajada deveria ativar todo o estoque de {_pool.GruntPoolSize}; ativos: {_pool.ActiveCount}.");

            // A verificação prova que o estoque inteiro está ativo ao mesmo
            // tempo; devolvê-los antes do encerramento deixa a própria sonda
            // sem estado temporário e evita confundir isso com vazamento.
            foreach (var inimigo in _inimigosDaRajada)
                _pool.Release(inimigo);
            _inimigosDaRajada.Clear();
            Verificar(_pool.ActiveCount == _ativosAntesDoTeste,
                "a limpeza da sonda deveria devolver o pool à contagem inicial.");
            return;
        }

        if (_quadroDaFase < 5)
            return;

        Concluir();
    }

    /// <summary>Confere que uma instância recém-adquirida está indistinguível de um inimigo recém-criado.</summary>
    private void VerificarInstanciaLimpa(CharacterController grunt, string contexto)
    {
        var cerebro = grunt.Context!.EnemyBrain!;
        var vida = grunt.Context.Health!;
        var movimento = grunt.Context.Movement!;
        var combate = grunt.Context.Combat!;

        Verificar(cerebro.Estado == EnemyState.Idle,
            $"[{contexto}] deveria nascer em Idle, sem herdar o estado da vida anterior; está {cerebro.Estado}");
        Verificar(vida.Percent >= 0.999f, $"[{contexto}] vida deveria estar cheia; está {vida.Percent:P0}");
        Verificar(vida.IsAlive, $"[{contexto}] deveria estar vivo");
        Verificar(!vida.IsInvulnerable, $"[{contexto}] não deveria carregar i-frames de uma vida anterior");
        Verificar(movimento.IsGroundedConfiavel, $"[{contexto}] deveria estar assentado no chão, não com impulso de uma vida anterior");
        Verificar(!combate.IsAttacking, $"[{contexto}] não deveria estar no meio de um golpe de uma vida anterior");
        Verificar(grunt.Visible, $"[{contexto}] deveria estar visível");
        Verificar(grunt.ProcessMode != Node.ProcessModeEnum.Disabled, $"[{contexto}] deveria estar processando de novo");
        Verificar(grunt.CollisionLayer != 0 && grunt.CollisionMask != 0,
            $"[{contexto}] a colisão deveria ter voltado -- ainda zerada da morte anterior");
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[pool-inimigo] FALHA: {mensagem}");
    }

    private void AvancarFase(Fase proxima)
    {
        _fase = proxima;
        _quadroDaFase = 0;
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[pool-inimigo] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[pool-inimigo] todas as verificações passaram ({CiclosDeStress} ciclos reais de Acquire/Release)");
        GetTree().Quit();
    }
}
