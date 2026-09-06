using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Combat;
using Contenda.Components.Health;
using Contenda.Core;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o inimigo lançado no ar pelo anti-aéreo na árvore de nós real,
/// sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem <c>EnemyStateMachine</c> isolada -- as
/// transições de e para <see cref="EnemyState.Airborne"/> em si. O que eles
/// não alcançam é a fiação: se um lançamento vertical DE VERDADE (dano +
/// impulso, como <c>UppercutBehavior</c> realmente aplica) entra no ar, se o
/// grunt genuinamente para de andar e de atacar enquanto voa, se golpes
/// comuns do combo aéreo (ticket 19) o sustentam sem derrubar para
/// atordoado, se aterrissar de verdade devolve à perseguição, e se resetar
/// no meio do ar não deixa ninguém "preso" flutuando. Ticket 24, spec 16 §6.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/AerialEnemyProbe.tscn
/// </code>
/// </remarks>
public sealed partial class AerialEnemyProbe : Node
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
    private CombatComponent? _combateDoGrunt;

    private enum Fase { Lancamento, Sustentado, Aterrissagem, ResetNoAr }
    private Fase _fase = Fase.Lancamento;
    private int _quadroDaFase;

    private Vector3 _posicaoAoLancar;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        var enemyPacked = GD.Load<PackedScene>(EnemyScenePath);
        if (packed is null || enemyPacked is null)
        {
            GD.PrintErr($"[aereo-inimigo] não consegui carregar {ScenePath} ou {EnemyScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        _grunt = (CharacterController)enemyPacked.Instantiate();
        AddChild(_grunt);

        if (_jogador?.Context?.Health is null || _grunt.Context?.Health is null || _grunt.Context.Movement is null)
        {
            GD.PrintErr("[aereo-inimigo] FALHA: jogador ou grunt sem Context/Health/Movement.");
            GetTree().Quit(1);
            return;
        }

        _cerebro = _grunt.GetNodeOrNull<EnemyBrain>("EnemyBrain");
        _combateDoGrunt = _grunt.Context.Combat;

        if (_cerebro is null || _combateDoGrunt is null)
        {
            GD.PrintErr("[aereo-inimigo] FALHA: grunt sem EnemyBrain/CombatComponent.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[aereo-inimigo] jogador e grunt prontos");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_cerebro is null)
            return;

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.Lancamento: TickLancamento(); break;
            case Fase.Sustentado: TickSustentado(); break;
            case Fase.Aterrissagem: TickAterrissagem(); break;
            case Fase.ResetNoAr: TickResetNoAr(); break;
            default: break;
        }
    }

    private void TickLancamento()
    {
        if (_quadroDaFase == 1)
        {
            Resetar();

            // Bem perto do jogador -- de propósito: se o lançamento ainda
            // deixasse o grunt "dentro do alcance de ataque" contar para
            // alguma coisa, seria aqui que apareceria.
            Teleportar(_jogador!, new Vector3(0f, 0.1f, 1.5f));
            Teleportar(_grunt!, new Vector3(0f, 0.1f, 0f));
            return;
        }

        if (_quadroDaFase == 5)
        {
            // Mesma assinatura de UppercutBehavior: Direction = Vector3.Up,
            // mais o impulso de verdade que só ApplyKnockback aplica --
            // ApplyDamage sozinho não move ninguém.
            _posicaoAoLancar = _grunt!.GlobalPosition;
            _grunt.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: 1f, Type: DamageType.Physical, HitPoint: _grunt.GlobalPosition, Direction: Vector3.Up,
                Knockback: 0f, SourceId: 0UL, SourceTag: "aerial_enemy_probe", IsCritical: false));
            _grunt.Context.Movement!.ApplyKnockback(Vector3.Up * 2f);
            return;
        }

        if (_quadroDaFase < 8)
            return;

        Verificar(_cerebro!.Estado == EnemyState.Airborne,
            $"o lançamento vertical deveria ter mandado o grunt para o ar; está {_cerebro.Estado}");
        Verificar(!_grunt!.Context!.Movement!.IsGrounded,
            "o grunt deveria estar fisicamente no ar depois do impulso, não só no ESTADO certo");

        AvancarFase(Fase.Sustentado);
    }

    private void TickSustentado()
    {
        // Enquanto ainda estiver subindo/no auge, golpes COMUNS (o combo
        // aéreo do ticket 19, direção horizontal) não deveriam derrubar para
        // Staggered nem soltar do ar -- só sustentam.
        if (_quadroDaFase == 5)
        {
            _grunt!.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: 1f, Type: DamageType.Physical, HitPoint: _grunt.GlobalPosition, Direction: Vector3.Forward,
                Knockback: 0f, SourceId: 0UL, SourceTag: "aerial_enemy_probe", IsCritical: false));
            return;
        }

        if (_quadroDaFase == 7)
        {
            Verificar(_cerebro!.Estado == EnemyState.Airborne,
                $"um golpe comum não deveria tirar o grunt do ar; está {_cerebro.Estado}");
        }

        if (_quadroDaFase < 20)
            return;

        // Ainda no ar: não deveria ter avançado na direção do jogador (a
        // perseguição está desligada) nem tentado atacar, mesmo que o
        // jogador esteja bem perto.
        var deslocamentoNoPlano = new Vector3(
            _grunt!.GlobalPosition.X - _posicaoAoLancar.X, 0f, _grunt.GlobalPosition.Z - _posicaoAoLancar.Z);
        Verificar(deslocamentoNoPlano.Length() < 0.3f,
            $"o grunt não deveria perseguir enquanto está no ar; andou {deslocamentoNoPlano.Length():0.00} m no plano");
        Verificar(!_combateDoGrunt!.IsAttacking, "o grunt não deveria atacar enquanto está no ar");
        Verificar(_cerebro!.Estado == EnemyState.Airborne,
            $"o grunt deveria continuar no ar -- \"no ar\" só sai ao tocar o chão, nunca por tempo; está {_cerebro.Estado}");

        AvancarFase(Fase.Aterrissagem);
    }

    private void TickAterrissagem()
    {
        // Bastante tempo para a queda de verdade terminar (gravidade normal,
        // sem nenhum golpe novo segurando no alto).
        if (_quadroDaFase < 180)
            return;

        Verificar(_grunt!.Context!.Movement!.IsGrounded, "o grunt deveria ter aterrissado por essa altura");
        Verificar(_cerebro!.Estado != EnemyState.Airborne,
            $"aterrissar deveria tirar o grunt do ar; ainda está {_cerebro.Estado}");

        AvancarFase(Fase.ResetNoAr);
    }

    private void TickResetNoAr()
    {
        if (_quadroDaFase == 1)
        {
            // Lança de novo e, desta vez, recicla NO MEIO do voo -- "reciclar
            // um inimigo que morreu no ar não o traz de volta flutuando".
            _grunt!.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: 1f, Type: DamageType.Physical, HitPoint: _grunt.GlobalPosition, Direction: Vector3.Up,
                Knockback: 0f, SourceId: 0UL, SourceTag: "aerial_enemy_probe", IsCritical: false));
            _grunt.Context.Movement!.ApplyKnockback(Vector3.Up * 2f);
            return;
        }

        if (_quadroDaFase == 5)
        {
            Verificar(_cerebro!.Estado == EnemyState.Airborne, "o segundo lançamento também deveria valer");

            _cerebro!.ResetForSpawn();

            // Checa NO MESMO quadro do reset, antes que qualquer `Poll`
            // rode de novo: o jogador está bem ao lado de propósito, e um
            // alvo visível reage a partir de `Idle` tão rápido quanto
            // qualquer outro quadro -- esperar mais testaria a percepção,
            // não o reset em si.
            Verificar(_cerebro.Estado == EnemyState.Idle,
                $"reciclar no meio do ar deveria voltar ao estado de recém-criado (Idle), não continuar no ar; está {_cerebro.Estado}");

            Concluir();
        }
    }

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
        GD.PrintErr($"[aereo-inimigo] FALHA: {mensagem}");
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
            GD.PrintErr($"[aereo-inimigo] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[aereo-inimigo] todas as verificações passaram");
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
