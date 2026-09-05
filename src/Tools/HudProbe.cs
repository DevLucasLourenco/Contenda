using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.UI.HUD;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica a fiação do HUD na árvore real, sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem a <c>DamageLayerState</c> isolada, com controle
/// exato do relógio. O que eles não alcançam é a fiação: se o
/// <c>GameBootstrap</c> realmente adiciona o HUD à árvore, se o
/// <c>HudController</c> encontra o jogador pelo grupo <c>"player"</c>, se as
/// barras refletem dano e gasto de mana de verdade, e se a tecla de debug
/// troca o arquétipo e as duas barras acompanham.
///
/// O HUD já existe quando este probe roda — é o <c>GameBootstrap</c> (um
/// autoload) quem o adiciona, em qualquer cena. Este probe só o ENCONTRA.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/HudProbe.tscn
/// </code>
/// </remarks>
public sealed partial class HudProbe : Node
{
    /// <summary>Cena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private HealthBar? _barraDeVida;
    private ManaBar? _barraDeMana;
    private int _quadro;
    private float _vidaMaximaAntesDaTroca;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[hud] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
    }

    public override void _PhysicsProcess(double delta)
    {
        _quadro++;

        // Localizado por quadro, até aparecer: o GameBootstrap adiciona o HUD
        // e o HudController acha o jogador pelo grupo, ambos sem ordem
        // garantida em relação a este probe. Ver comentário do HudController.
        if (_jogador is null || _barraDeVida is null || _barraDeMana is null)
        {
            Localizar();
            return;
        }

        // `!` daqui em diante em _jogador.Context/.Health/.Mana: o guard acima
        // só libera este ramo depois de Localizar() confirmar os três não
        // nulos (ver o pattern match em ProcurarHud/Localizar).
        switch (_quadro)
        {
            case 10:
                Verificar(_barraDeVida.CurrentFraction > 0.99f, $"vida cheia deveria ser fração ~1, foi {_barraDeVida.CurrentFraction}");
                Verificar(_barraDeMana.CurrentFraction > 0.99f, $"mana cheia deveria ser fração ~1, foi {_barraDeMana.CurrentFraction}");

                // Golpe direto na vida, fora do combate: isola a fiação do
                // HUD da varredura de alvos do CombatComponent.
                _jogador.Context!.Health!.ApplyDamage(new DamageInfo(
                    Amount: 50f, Type: DamageType.Physical, HitPoint: Vector3.Zero,
                    Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL,
                    SourceTag: "hud_probe", IsCritical: false));
                break;

            case 11:
                // Um quadro depois: o ponto único do quadro (HealthComponent.
                // ResolveQueue) já rodou pelo menos uma vez desde o golpe.
                Verificar(_barraDeVida.CurrentFraction < 0.99f,
                    $"a barra deveria ter caído depois do golpe, ficou em {_barraDeVida.CurrentFraction}");
                Verificar(_barraDeVida.DamageLayerFraction > _barraDeVida.CurrentFraction + 0.05f,
                    "a camada de dano deveria continuar acima do preenchimento logo após o golpe");
                break;

            case 40:
                // 30 quadros depois do golpe (0,5 s a 60 Hz): além dos 0,4 s
                // de DamageLayerCatchUp do Hud.tscn -- a camada de dano já
                // devia ter alcançado o preenchimento.
                Verificar(Mathf.Abs(_barraDeVida.DamageLayerFraction - _barraDeVida.CurrentFraction) < 0.02f,
                    $"a camada de dano deveria ter alcançado o preenchimento; dano={_barraDeVida.DamageLayerFraction}, atual={_barraDeVida.CurrentFraction}");

                _jogador.Context!.Mana!.TryConsume(60f);
                break;

            case 41:
                Verificar(_barraDeMana.CurrentFraction < 0.99f,
                    $"a barra de mana deveria refletir o gasto, ficou em {_barraDeMana.CurrentFraction}");
                break;

            case 50:
                _vidaMaximaAntesDaTroca = _jogador.Context!.Health!.Max;

                // Tecla de debug: pressiona e solta, como um clique real.
                Godot.Input.ActionPress(InputActionNames.DebugSwitchCharacter);
                break;

            case 51:
                Godot.Input.ActionRelease(InputActionNames.DebugSwitchCharacter);
                break;

            case 55:
                var vidaMaximaDepois = _jogador.Context!.Health!.Max;
                Verificar(!Mathf.IsEqualApprox(vidaMaximaDepois, _vidaMaximaAntesDaTroca),
                    $"a tecla de debug deveria ter trocado o arquétipo (e o MaxHealth); continuou {vidaMaximaDepois}");

                // As duas barras continuam mostrando frações válidas depois da
                // troca -- nenhum NaN nem valor fora de [0,1] por causa da
                // reconfiguração em runtime.
                Verificar(_barraDeVida.CurrentFraction is >= 0f and <= 1f,
                    $"fração de vida inválida depois da troca: {_barraDeVida.CurrentFraction}");
                Verificar(_barraDeMana.CurrentFraction is >= 0f and <= 1f,
                    $"fração de mana inválida depois da troca: {_barraDeMana.CurrentFraction}");
                break;

            case 60:
                Concluir();
                break;

            default:
                break;
        }
    }

    private void Localizar()
    {
        // O grupo "player" é aplicado quando o nó ENTRA na árvore, antes do
        // próprio _Ready dele -- o mesmo instante em que Context é montado.
        // Só aceita o achado depois que Context existir de verdade, senão o
        // guard do _PhysicsProcess liberaria acesso a Context nulo.
        if (_jogador is null
            && GetTree().GetFirstNodeInGroup(NodeGroups.Player) is CharacterController candidato
            && candidato.Context is not null)
        {
            _jogador = candidato;
        }

        if (_barraDeVida is null || _barraDeMana is null)
            ProcurarHud(GetTree().Root);
    }

    private void ProcurarHud(Node no)
    {
        if (no is HealthBar vida)
            _barraDeVida = vida;
        else if (no is ManaBar mana)
            _barraDeMana = mana;

        if (_barraDeVida is not null && _barraDeMana is not null)
            return;

        // Fronteira com a engine: GetChildren() devolve Godot.Collections.Array.
        // Convenções §5 proíbe isto em _PhysicsProcess, mas a varredura inteira
        // só roda por 1-2 quadros no boot, até o HUD aparecer -- depois disso
        // Localizar() para de chamar este método. Ferramenta de depuração,
        // fora do export.
        foreach (var filho in no.GetChildren())
            ProcurarHud(filho);
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[hud] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[hud] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[hud] todas as verificações passaram");
        GetTree().Quit();
    }
}
