using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Abilities;
using Contenda.Core;
using Contenda.Input;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica a sequência-confirma-executa de ponta a ponta na árvore de nós real.
/// </summary>
/// <remarks>
/// O momento go/no-go do ticket 14. xUnit já cobre <c>AbilityCooldownTracker</c>,
/// <c>CommandBuffer</c> e <c>AbilityComboResolver</c> isolados; o que só dá para
/// verificar aqui é a fiação inteira: se o `.tres` da habilidade carrega, se
/// M2 realmente resolve a sequência acumulada, se a mana só desconta depois de
/// todos os gates passarem, se a recarga bloqueia reexecução e se morrer a meio
/// do cast cancela sem aplicar dano nenhum. Nenhum dos dois — <c>AbilityDefinition</c>
/// é <c>Resource</c> — pode ser construído em xUnit.
///
/// O jogador vem de <c>Character.tscn</c> já equipado com o Swordsman (dono do
/// Dash Slash); o alvo é o manequim resiliente, parado bem no caminho do dash,
/// para isolar o teste da geometria exata de mira -- mesma escolha do
/// <c>RevolverProbe</c> quanto a não precisar de teclado nenhum.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/AbilityProbe.tscn
/// </code>
/// </remarks>
public sealed partial class AbilityProbe : Node
{
    /// <summary>Cena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/debug/AbilityArena.tscn";

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private CharacterController? _alvo;
    private AbilityComponent? _habilidades;
    private AbilityDefinition? _dashSlash;
    private int _quadro;

    private float _manaAntes;
    private float _vidaAlvoAntes;
    private Vector3 _posicaoAntesDoDash;
    private int _rejeicoes;
    private int _sequenciasRejeitadas;
    private int _ataquesBasicosDisparados;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[habilidade] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        _habilidades = _jogador?.Context?.Abilities;
        if (_habilidades is null || _jogador?.Context?.Mana is null
            || _jogador?.Context?.Health is null || _alvo?.Context?.Health is null)
        {
            GD.PrintErr("[habilidade] FALHA: jogador sem AbilityComponent/ManaComponent/HealthComponent, ou alvo sem vida.");
            GetTree().Quit(1);
            return;
        }

        if (_habilidades.Abilities.Count == 0)
        {
            GD.PrintErr("[habilidade] FALHA: swordsman sem nenhuma AbilityDefinition configurada.");
            GetTree().Quit(1);
            return;
        }

        _dashSlash = _habilidades.Abilities[0];
        _habilidades.Rejected += (_, _) => _rejeicoes++;
        _habilidades.SequenceRejected += () => _sequenciasRejeitadas++;
        _jogador.Context.Combat!.AttackStarted += _ => _ataquesBasicosDisparados++;

        GD.Print($"[habilidade] jogador e alvo prontos; habilidade '{_dashSlash.DisplayName}'");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_habilidades is null || _dashSlash is null)
            return;

        var mana = _jogador?.Context?.Mana;
        var vidaAlvo = _alvo?.Context?.Health;
        if (mana is null || vidaAlvo is null)
            return;

        _quadro++;

        switch (_quadro)
        {
            case 9:
                // O Swordsman usa FaceMode.Aim por padrão (ADR-011): a frente
                // real do corpo depende de onde a mira projeta o cursor da
                // tela, não de um -Z fixo. Reposiciona o alvo NA frente real,
                // um quadro antes de confirmar -- dá tempo do giro (14 rad/s)
                // convergir primeiro, e isola o teste de acerto/dano da
                // direção exata que o modo de mira escolhe, que não é o que
                // este ticket verifica.
                RealocarAlvoNaFrenteDoJogador();
                break;

            case 10:
                _manaAntes = mana.Current;
                _vidaAlvoAntes = vidaAlvo.Current;
                _posicaoAntesDoDash = _jogador!.GlobalPosition;
                ExecutarSequencia();
                Verificar(_habilidades.Executing == _dashSlash, "a execução deveria começar na hora da confirmação");
                Verificar(Mathf.IsEqualApprox(mana.Current, _manaAntes - _dashSlash.ManaCost),
                    $"mana deveria descontar {_dashSlash.ManaCost} na hora; ficou em {mana.Current}, era {_manaAntes}");
                break;

            case 20:
                // CastTime (0,10 s) já passou -- o dash deve ter resolvido: o
                // alvo, parado no caminho, perde vida e o jogador avança.
                Verificar(vidaAlvo.Current < _vidaAlvoAntes,
                    $"o dash deveria ter acertado o alvo; vida ficou em {vidaAlvo.Current}");
                var deslocamento = _jogador!.GlobalPosition.DistanceTo(_posicaoAntesDoDash);
                Verificar(deslocamento > 4f,
                    $"o dash deveria ter avançado ~{_dashSlash.DashDistance} m; avançou só {deslocamento:0.00} m");
                break;

            case 40:
                // CastTime + RecoveryTime (0,35 s) já passou -- de volta a ocioso.
                Verificar(_habilidades.Executing is null, "a execução deveria ter terminado sozinha");

                var antesDoCooldown = _rejeicoes;
                var resultado = _habilidades.TryExecute(_dashSlash);
                Verificar(resultado == AbilityAttemptResult.OnCooldown,
                    $"reexecutar em recarga deveria falhar com OnCooldown; veio {resultado}");
                Verificar(_rejeicoes == antesDoCooldown + 1, "OnCooldown deveria disparar Rejected");
                Verificar(_habilidades.CooldownRemaining(_dashSlash.Id) > 0f,
                    "CooldownRemaining deveria ser positivo em recarga");
                break;

            case 200:
                // Recarga de 3 s (iniciada no quadro 10) já passou de sobra.
                Verificar(_habilidades.IsReady(_dashSlash.Id), "a habilidade deveria estar pronta de novo");

                // Drena a mana artificialmente, sem tocar no `.tres`: assim o
                // gate de mana é testado sem depender do relógio de
                // regeneração real ter chegado a um valor exato.
                mana.Drain(mana.Current - 5f);
                var antesDaFalta = _rejeicoes;
                var manaAntesDaFalta = mana.Current;
                var resultadoFalta = _habilidades.TryExecute(_dashSlash);
                Verificar(resultadoFalta == AbilityAttemptResult.NotEnoughMana,
                    $"sem mana suficiente deveria falhar com NotEnoughMana; veio {resultadoFalta}");
                Verificar(_rejeicoes == antesDaFalta + 1, "NotEnoughMana deveria disparar Rejected");
                Verificar(Mathf.IsEqualApprox(mana.Current, manaAntesDaFalta),
                    "nada deveria ser consumido quando a mana é insuficiente");
                Verificar(_habilidades.Executing is null, "uma tentativa reprovada não deveria começar execução nenhuma");
                break;

            case 205:
                // O primeiro dash deslocou o jogador uns 6 m; realoca o alvo
                // de novo na frente real, na posição atual.
                RealocarAlvoNaFrenteDoJogador();
                break;

            case 210:
                mana.Restore(100f);
                _vidaAlvoAntes = vidaAlvo.Current;
                ExecutarSequencia();
                Verificar(_habilidades.Executing == _dashSlash, "a segunda execução deveria começar normalmente");
                break;

            case 250:
                Verificar(_habilidades.Executing is null, "a segunda execução deveria ter terminado sozinha");
                Verificar(vidaAlvo.Current < _vidaAlvoAntes, "a segunda execução deveria ter acertado de novo");

                var basicosAntes = _ataquesBasicosDisparados;
                var sequenciasAntes = _sequenciasRejeitadas;

                // Direita-Esquerda: não é a sequência de nenhuma habilidade do
                // Swordsman (Esquerda-Direita É o Spin Slash, ticket 15) --
                // de propósito, para continuar testando "sem match nenhum".
                _habilidades.PushToken(CommandDirection.Right);
                _habilidades.PushToken(CommandDirection.Left);
                _habilidades.RequestConfirm();
                Verificar(_sequenciasRejeitadas == sequenciasAntes + 1,
                    "confirmar uma sequência sem match deveria disparar SequenceRejected");
                Verificar(_ataquesBasicosDisparados == basicosAntes,
                    "confirmar sem match NUNCA deveria cair no ataque básico como consolo");
                Verificar(_habilidades.CurrentSequence.Length == 0, "a fila deveria esvaziar depois da falta de match");
                break;

            case 400:
                // A segunda recarga (iniciada no quadro 210) já passou de sobra.
                Verificar(_habilidades.IsReady(_dashSlash.Id), "a habilidade deveria estar pronta para o teste de cancelamento");
                ExecutarSequencia();
                Verificar(_habilidades.Executing == _dashSlash, "a terceira execução deveria começar normalmente");
                break;

            case 402:
                // Ainda dentro do CastTime (0,10 s = ~6 quadros a 60 Hz): matar
                // agora testa cancelamento a meio do cast, antes do dash resolver.
                _vidaAlvoAntes = vidaAlvo.Current;
                _jogador!.Context!.Health!.Kill("ability_probe");
                break;

            case 405:
                Verificar(_habilidades.Executing is null, "morrer deveria cancelar a execução na hora");
                Verificar(Mathf.IsEqualApprox(vidaAlvo.Current, _vidaAlvoAntes),
                    "uma execução cancelada antes do cast terminar não deveria ter aplicado dano nenhum");
                break;

            case 420:
                Concluir();
                break;

            default:
                break;
        }
    }

    private void RealocarAlvoNaFrenteDoJogador()
    {
        var frenteReal = -_jogador!.GlobalTransform.Basis.Z;
        _alvo!.GlobalPosition = _jogador.GlobalPosition + (frenteReal.Normalized() * 3f);
    }

    private void ExecutarSequencia()
    {
        _habilidades!.PushToken(CommandDirection.Up);
        _habilidades.PushToken(CommandDirection.Up);
        _habilidades.RequestConfirm();
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[habilidade] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[habilidade] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[habilidade] todas as verificações passaram");
        GetTree().Quit();
    }

    private void Procurar(Node no)
    {
        foreach (var filho in no.GetChildren())
        {
            if (filho is CharacterController c)
            {
                if (c.Team == Team.Player && c.Context?.Abilities is not null)
                    _jogador ??= c;
                else if (c.Team == Team.Enemy && c.Context?.Health is not null)
                    _alvo ??= c;
            }

            Procurar(filho);
        }
    }
}
