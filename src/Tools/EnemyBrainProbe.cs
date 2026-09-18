using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Combat;
using Contenda.Components.Health;
using Contenda.Core;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o inimigo que percebe, persegue e ataca na árvore de nós real,
/// sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem <c>EnemyStateMachine</c> isolada -- as
/// transições em si. O que eles não alcançam é a fiação: se a percepção de
/// verdade (raio + raycast) decide certo, se a navegação de verdade contorna
/// obstáculo, se o windup realmente acende um aviso ANTES de causar dano, se
/// apanhar interrompe o ataque em andamento, e se perder o alvo por tempo
/// demais desiste de verdade. Ticket 22, spec 09.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/EnemyBrainProbe.tscn
/// </code>
/// </remarks>
public sealed partial class EnemyBrainProbe : Node
{
    /// <summary>Cena com o jogador.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    /// <summary>Cena do inimigo a testar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string EnemyScenePath { get; set; } = "res://scenes/characters/EnemyGrunt.tscn";

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private CharacterController? _grunt;
    private EnemyBrain? _cerebro;
    private AttackTelegraphComponent? _telegrafo;
    private CombatComponent? _combateDoGrunt;

    private enum Fase { Ocioso, Alerta, Parede, Perseguicao, Ataque, Interromper, PerdaDeAlvo }
    private Fase _fase = Fase.Ocioso;
    private int _quadroDaFase;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        var enemyPacked = GD.Load<PackedScene>(EnemyScenePath);
        if (packed is null || enemyPacked is null)
        {
            GD.PrintErr($"[ia] não consegui carregar {ScenePath} ou {EnemyScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        _grunt = (CharacterController)enemyPacked.Instantiate();
        AddChild(_grunt);

        if (_jogador?.Context?.Health is null || _grunt.Context?.Health is null)
        {
            GD.PrintErr("[ia] FALHA: jogador ou grunt sem Context/Health.");
            GetTree().Quit(1);
            return;
        }

        _cerebro = _grunt.GetNodeOrNull<EnemyBrain>("EnemyBrain");
        _telegrafo = _grunt.GetNodeOrNull<AttackTelegraphComponent>("AttackTelegraphComponent");
        _combateDoGrunt = _grunt.Context.Combat;

        if (_cerebro is null || _telegrafo is null || _combateDoGrunt is null)
        {
            GD.PrintErr("[ia] FALHA: grunt sem EnemyBrain/AttackTelegraphComponent/CombatComponent.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[ia] jogador e grunt prontos");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_cerebro is null)
            return;

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.Ocioso: TickOcioso(); break;
            case Fase.Alerta: TickAlerta(); break;
            case Fase.Parede: TickParede(); break;
            case Fase.Perseguicao: TickPerseguicao(); break;
            case Fase.Ataque: TickAtaque(); break;
            case Fase.Interromper: TickInterromper(); break;
            case Fase.PerdaDeAlvo: TickPerdaDeAlvo(); break;
            default: break;
        }
    }

    private void TickOcioso()
    {
        if (_quadroDaFase == 1)
        {
            // Bem além do raio de detecção (22 m) e do de perda (30 m).
            Resetar();
            Teleportar(_jogador!, new Vector3(0f, 0.1f, 100f));
            Teleportar(_grunt!, new Vector3(0f, 0.1f, 0f));
        }

        if (_quadroDaFase < 20)
            return;

        Verificar(_cerebro!.Estado == EnemyState.Idle,
            $"o grunt deveria ignorar o jogador fora do raio de detecção; está {_cerebro.Estado}");

        AvancarFase(Fase.Alerta);
    }

    private void TickAlerta()
    {
        if (_quadroDaFase == 1)
        {
            // Dentro do raio de detecção, linha de visão livre.
            Resetar();
            Teleportar(_jogador!, new Vector3(0f, 0.1f, 15f));
            Teleportar(_grunt!, new Vector3(0f, 0.1f, 0f));
            return;
        }

        if (_quadroDaFase == 5)
        {
            Verificar(_cerebro!.Estado == EnemyState.Alert,
                $"o grunt deveria alertar ao ver o jogador; está {_cerebro.Estado}");
            return;
        }

        // 0,4 s de alerta = 24 quadros; folga generosa.
        if (_quadroDaFase < 40)
            return;

        Verificar(_cerebro!.Estado == EnemyState.Chase,
            $"depois do alerta o grunt deveria partir para a perseguição; está {_cerebro.Estado}");

        AvancarFase(Fase.Parede);
    }

    private void TickParede()
    {
        if (_quadroDaFase == 1)
        {
            // Reseta para Idle ANTES de posicionar: senão o estado Chase
            // herdado da fase anterior usaria o raio de PERDA (maior, e com
            // tolerância de alguns segundos antes de desistir) em vez do
            // raio de DETECÇÃO -- o teste passaria por engano, tarde demais,
            // em vez de nunca perceber através da parede.
            Resetar();

            // VielaParede (Arena.tscn, ticket 21): parede de 1×3×7 m em
            // x ∈ [-12, -11] -- alta o bastante para bloquear a visão na
            // altura dos olhos. Jogador e grunt em lados opostos dela, 6 m
            // do centro cada, 6 m entre si -- dentro do raio de detecção
            // (22 m), mas sem linha de visão nenhuma.
            Teleportar(_jogador!, new Vector3(-14f, 0.1f, 12.5f));
            Teleportar(_grunt!, new Vector3(-8f, 0.1f, 12.5f));
            return;
        }

        if (_quadroDaFase < 20)
            return;

        Verificar(_cerebro!.Estado == EnemyState.Idle,
            $"o grunt não deveria perceber o jogador através de uma parede; está {_cerebro.Estado}");

        AvancarFase(Fase.Perseguicao);
    }

    private void TickPerseguicao()
    {
        if (_quadroDaFase == 1)
        {
            Resetar();
            Teleportar(_jogador!, new Vector3(0f, 0.1f, 15f));
            Teleportar(_grunt!, new Vector3(0f, 0.1f, 0f));
            return;
        }

        // Passa por Alert antes de perseguir de verdade -- espera os dois.
        if (_quadroDaFase == 45)
        {
            Verificar(_cerebro!.Estado == EnemyState.Chase,
                $"o grunt deveria estar perseguindo; está {_cerebro.Estado}");
            return;
        }

        if (_quadroDaFase < 105) // mais ~1 s de perseguição de verdade
            return;

        var distanciaFinal = _grunt!.GlobalPosition.DistanceTo(_jogador!.GlobalPosition);
        Verificar(distanciaFinal < 15f - 0.5f,
            $"o grunt deveria ter se aproximado do jogador perseguindo; distância ainda {distanciaFinal:0.0} m");

        AvancarFase(Fase.Ataque);
    }

    private void TickAtaque()
    {
        if (_quadroDaFase == 1)
        {
            Resetar();
            Teleportar(_jogador!, new Vector3(0f, 0.1f, 1.5f));
            Teleportar(_grunt!, new Vector3(0f, 0.1f, 0f));
            return;
        }

        // Precisa passar por Alert (0,4 s) antes de sequer poder atacar.
        if (_quadroDaFase == 5)
        {
            Verificar(_cerebro!.Estado != EnemyState.Idle,
                "o grunt deveria ter percebido o jogador já bem perto");
            return;
        }

        if (_quadroDaFase == 30)
        {
            Verificar(_cerebro!.Estado == EnemyState.Attack,
                $"a essa distância o grunt já deveria estar atacando; está {_cerebro!.Estado}");
            return;
        }

        // Ainda dentro do windup (0,45 s = 27 quadros a partir de Attack,
        // então por volta do quadro 30+15=45 ainda deveria estar avisando,
        // sem ter causado dano nenhum).
        if (_quadroDaFase == 45)
        {
            Verificar(_telegrafo!.IsWarning, "o golpe deveria estar avisando (telegrafia) antes de causar dano");
            Verificar(_jogador!.Context!.Health!.Current >= _jogador.Context.Health.Max - 0.01f,
                "o golpe não deveria ter causado dano ainda, ainda em preparação");
            return;
        }

        // Windup (0,45s) + janela de acerto do passo (0,2-0,35s) com folga --
        // por volta do quadro 30+27+25=82 o golpe já devia ter conectado.
        if (_quadroDaFase < 95)
            return;

        Verificar(_jogador!.Context!.Health!.Current < _jogador.Context.Health.Max - 0.01f,
            "o golpe do grunt deveria ter causado dano no jogador");
        Verificar(!_telegrafo!.IsWarning, "o aviso deveria ter apagado depois do golpe");
        Verificar(_cerebro!.Estado is EnemyState.Recover or EnemyState.Chase,
            $"depois de golpear o grunt deveria estar recuperando (ou já ter voltado a perseguir); está {_cerebro.Estado}");

        AvancarFase(Fase.Interromper);
    }

    private void TickInterromper()
    {
        if (_quadroDaFase == 1)
        {
            Resetar();
            Teleportar(_jogador!, new Vector3(0f, 0.1f, 1.5f));
            Teleportar(_grunt!, new Vector3(0f, 0.1f, 0f));
            return;
        }

        // Espera o grunt entrar em Attack de novo (Alert 0,4s + reaproximação).
        if (_quadroDaFase == 60 && _cerebro!.Estado != EnemyState.Attack)
        {
            Verificar(false, $"o grunt deveria ter voltado a atacar para este teste; está {_cerebro.Estado}");
            AvancarFase(Fase.PerdaDeAlvo);
            return;
        }

        if (_quadroDaFase == 60)
        {
            // Apanha NO MEIO do que quer que esteja fazendo (windup ou golpe
            // de verdade) -- "apanhar o interrompe" não deveria depender de
            // pegar o instante exato.
            _grunt!.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: 1f, Type: DamageType.Physical, HitPoint: _grunt.GlobalPosition, Direction: Vector3.Forward,
                Knockback: 0f, SourceId: 0UL, SourceTag: "enemy_brain_probe", IsCritical: false));
            return;
        }

        if (_quadroDaFase == 62)
        {
            Verificar(_cerebro!.Estado == EnemyState.Staggered,
                $"apanhar deveria atordoar o grunt na hora; está {_cerebro.Estado}");
            Verificar(!_telegrafo!.IsWarning, "atordoar deveria apagar o aviso de golpe em andamento");
            Verificar(!_combateDoGrunt!.IsAttacking, "atordoar deveria cancelar um golpe em andamento");
            return;
        }

        // O golpe conectou por volta do quadro 60; 0,5 s de atordoamento a
        // partir dali = +30 quadros = quadro 90. Folga generosa por cima.
        if (_quadroDaFase < 105)
            return;

        // Não necessariamente "Chase" -- o jogador nunca saiu do alcance de
        // ataque, então voltar a perseguir e imediatamente reengajar (Attack
        // de novo) é um comportamento correto, não um bug. O que este teste
        // precisa provar é só que o atordoamento em si TERMINOU sozinho.
        Verificar(_cerebro!.Estado != EnemyState.Staggered,
            $"o atordoamento deveria ter terminado sozinho por essa altura; ainda está {_cerebro.Estado}");

        AvancarFase(Fase.PerdaDeAlvo);
    }

    private void TickPerdaDeAlvo()
    {
        if (_quadroDaFase == 1)
        {
            // Reseta em vez de confiar no estado herdado da fase anterior:
            // depois de um atordoamento, o grunt pode voltar direto para
            // Attack (o jogador nunca saiu do alcance) em vez de ficar
            // parado em Chase -- ver o comentário da fase anterior. Este
            // teste quer especificamente alguém em PERSEGUIÇÃO de verdade.
            Resetar();
            Teleportar(_jogador!, new Vector3(0f, 0.1f, 15f));
            Teleportar(_grunt!, new Vector3(0f, 0.1f, 0f));
            return;
        }

        // Espera entrar em perseguição de verdade antes de testar a perda de
        // alvo -- Alert (0,4 s = 24 quadros) com folga.
        if (_quadroDaFase == 45)
        {
            Verificar(_cerebro!.Estado == EnemyState.Chase,
                $"o grunt deveria estar perseguindo antes deste teste; está {_cerebro.Estado}");

            // Some de vista bem além do raio de perda (30 m).
            Teleportar(_jogador!, new Vector3(0f, 0.1f, 200f));
            return;
        }

        // 3 s de tolerância = 180 quadros a partir de quando sumiu (quadro
        // 45); um pouco antes ainda não devia ter desistido.
        if (_quadroDaFase == 45 + 150)
        {
            Verificar(_cerebro!.Estado != EnemyState.Idle,
                "o grunt não deveria desistir de perseguir antes do prazo de tolerância");
            return;
        }

        if (_quadroDaFase < 45 + 220)
            return;

        Verificar(_cerebro!.Estado == EnemyState.Idle,
            $"depois de perder o alvo por tempo demais o grunt deveria desistir; está {_cerebro.Estado}");

        Concluir();
    }

    /// <summary>Devolve os dois personagens e o cérebro do grunt a um estado limpo e conhecido.</summary>
    private void Resetar()
    {
        _cerebro!.ResetForSpawn();
        _jogador!.Context!.Health!.Heal(999999f);
        _grunt!.Context!.Health!.Heal(999999f);
    }

    private void Teleportar(CharacterController quem, Vector3 posicao)
    {
        quem.Velocity = Vector3.Zero;
        quem.GlobalPosition = posicao;
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[ia] FALHA: {mensagem}");
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
            GD.PrintErr($"[ia] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[ia] todas as verificações passaram");
        GetTree().Quit();
    }

    private void Procurar(Node no)
    {
        foreach (var filho in no.GetChildren())
        {
            if (filho is CharacterController c && c.Team == Team.Player && c.Context?.Combat is not null)
                _jogador ??= c;

            Procurar(filho);
        }
    }
}
