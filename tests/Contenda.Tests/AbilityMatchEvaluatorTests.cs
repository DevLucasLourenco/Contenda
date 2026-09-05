using Contenda.Input;
using Contenda.UI.HUD;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Estado visual de uma linha do guia de combos — ticket 16.
/// </summary>
public sealed class AbilityMatchEvaluatorTests
{
    private static readonly CommandDirection[] DashSlash = [CommandDirection.Up, CommandDirection.Up];
    private static readonly CommandDirection[] HeavyLunge = [CommandDirection.Up, CommandDirection.Left, CommandDirection.Up];

    [Fact]
    public void Buffer_vazio_e_neutro()
    {
        var estado = AbilityMatchEvaluator.Evaluate(DashSlash, []);
        Assert.Equal(AbilityMatchState.Neutral, estado);
    }

    [Fact]
    public void Prefixo_correto_e_parcial()
    {
        var estado = AbilityMatchEvaluator.Evaluate(HeavyLunge, [CommandDirection.Up]);
        Assert.Equal(AbilityMatchState.PartialMatch, estado);
    }

    [Fact]
    public void Sequencia_inteira_e_completa()
    {
        var estado = AbilityMatchEvaluator.Evaluate(DashSlash, [CommandDirection.Up, CommandDirection.Up]);
        Assert.Equal(AbilityMatchState.Complete, estado);
    }

    [Fact]
    public void Primeiro_simbolo_errado_e_impossivel()
    {
        var estado = AbilityMatchEvaluator.Evaluate(DashSlash, [CommandDirection.Down]);
        Assert.Equal(AbilityMatchState.Impossible, estado);
    }

    [Fact]
    public void Segundo_simbolo_errado_depois_de_um_prefixo_correto_e_impossivel()
    {
        var estado = AbilityMatchEvaluator.Evaluate(HeavyLunge, [CommandDirection.Up, CommandDirection.Right]);
        Assert.Equal(AbilityMatchState.Impossible, estado);
    }

    [Fact]
    public void Digitar_alem_da_sequencia_inteira_e_impossivel()
    {
        var estado = AbilityMatchEvaluator.Evaluate(DashSlash, [CommandDirection.Up, CommandDirection.Up, CommandDirection.Left]);
        Assert.Equal(AbilityMatchState.Impossible, estado);
    }

    [Fact]
    public void Duas_habilidades_com_o_mesmo_primeiro_simbolo_ficam_parciais_juntas()
    {
        // W sozinho é prefixo tanto do Dash Slash quanto do Heavy Lunge --
        // nenhum dos dois deveria "apagar" ainda.
        var digitado = new CommandDirection[] { CommandDirection.Up };

        Assert.Equal(AbilityMatchState.PartialMatch, AbilityMatchEvaluator.Evaluate(DashSlash, digitado));
        Assert.Equal(AbilityMatchState.PartialMatch, AbilityMatchEvaluator.Evaluate(HeavyLunge, digitado));
    }
}
