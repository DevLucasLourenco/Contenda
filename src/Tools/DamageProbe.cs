using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Components.Stats;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica a cadeia de dano na árvore de nós de verdade, sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem <c>HealthState</c> e <c>StatBlock</c> isolados. O
/// que eles **não** alcançam é a fiação: se o <c>HealthComponent</c> realmente
/// achou o <c>StatsComponent</c> no <c>Bind</c>, se o contêiner chama
/// <c>ResolveQueue</c>, se a mitigação usa a defesa certa. Isso só aparece com a
/// cena montada — e sem editor não há como apertar uma tecla para provar.
///
/// Rode assim:
/// <code>
/// godot --headless --path . --scene res://scenes/debug/DamageProbe.tscn
/// </code>
///
/// Sai com código 1 se qualquer verificação falhar, então serve em CI.
/// </remarks>
public sealed partial class DamageProbe : Node
{
    /// <summary>Cena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    private readonly List<string> _falhas = [];
    private CharacterController? _alvo;
    private int _quadro;
    private int _mortes;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[dano] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        _alvo = Procurar(this);

        if (_alvo?.Context?.Health is null || _alvo.Context.Stats is null)
        {
            GD.PrintErr("[dano] FALHA: manequim sem HealthComponent ou StatsComponent ligados. "
                        + "A fiação do Bind quebrou.");
            GetTree().Quit(1);
            return;
        }

        _alvo.Context.Health.Died += _ => _mortes++;
        GD.Print($"[dano] alvo: {_alvo.Name}, vida {_alvo.Context.Health.Current:0}");
    }

    public override void _PhysicsProcess(double delta)
    {
        var vida = _alvo?.Context?.Health;
        var stats = _alvo?.Context?.Stats;
        if (vida is null || stats is null)
            return;

        _quadro++;

        switch (_quadro)
        {
            case 1:
                // Dois golpes na MESMA fila, com dano de sobra para matar duas
                // vezes. Se a morte não for idempotente, aparece aqui.
                vida.ApplyDamage(Golpe(60f));
                vida.ApplyDamage(Golpe(60f));
                break;

            case 3:
                Verificar(_mortes == 1, $"morte disparou {_mortes}x, esperado 1");
                Verificar(!vida.IsAlive, "deveria estar morto");

                // Mitigação: defesa 2 corta o dano pela metade.
                vida.ResetForSpawn();
                stats.SetBase(StatId.DefenseMultiplier, 2f);
                vida.ApplyDamage(Golpe(40f));
                break;

            case 5:
                Verificar(Mathf.IsEqualApprox(vida.Current, 80f, 0.5f),
                    $"com defesa 2, 40 de dano deveria deixar 80; deixou {vida.Current:0.0}");

                // Invulnerabilidade: o segundo golpe imediato não conta.
                vida.ApplyDamage(Golpe(10f));
                break;

            case 7:
                Verificar(Mathf.IsEqualApprox(vida.Current, 80f, 0.5f),
                    $"golpe durante invulnerabilidade não deveria contar; vida {vida.Current:0.0}");

                // Reversão de modificador: tem que voltar ao valor base EXATO.
                stats.AddModifier(new StatModifier(
                    StatId.DefenseMultiplier, ModifierOp.PercentMult, 0.6f, "form:teste"));
                Verificar(Mathf.IsEqualApprox(stats.Get(StatId.DefenseMultiplier), 3.2f, 0.001f),
                    "modificador não aplicou");
                stats.RemoveBySource("form:teste");
                Verificar(stats.Get(StatId.DefenseMultiplier) == 2f,
                    $"reversão deixou resíduo: {stats.Get(StatId.DefenseMultiplier)}");
                break;

            case 9:
                Concluir();
                break;

            default:
                break;
        }
    }

    private static DamageInfo Golpe(float quanto) => new(
        quanto, DamageType.Physical, Vector3.Zero, Vector3.Forward,
        0f, 0UL, "probe", false);

    private void Verificar(bool condicao, string mensagemDeFalha)
    {
        if (condicao)
            return;

        _falhas.Add(mensagemDeFalha);
        GD.PrintErr($"[dano] FALHA: {mensagemDeFalha}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[dano] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[dano] todas as verificações passaram (5 casos na árvore real)");
        GetTree().Quit();
    }

    private static CharacterController? Procurar(Node no)
    {
        foreach (var filho in no.GetChildren())
        {
            if (filho is CharacterController c && c.Context?.Health is not null
                && c.Team == Core.Team.Enemy)
                return c;

            var achado = Procurar(filho);
            if (achado is not null)
                return achado;
        }

        return null;
    }
}
