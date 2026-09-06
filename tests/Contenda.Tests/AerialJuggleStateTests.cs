using Contenda.Components.Health;
using Xunit;

namespace Contenda.Tests;

/// <summary>Teto de acertos aéreos consecutivos — ticket 19, spec 16 §6.</summary>
public sealed class AerialJuggleStateTests
{
    [Fact]
    public void Primeiros_quatro_acertos_ficam_sob_o_teto()
    {
        var juggle = new AerialJuggleState();

        for (var i = 0; i < AerialJuggleState.LimiteDeAcertos; i++)
            Assert.True(juggle.RegistrarAcerto());
    }

    [Fact]
    public void Quinto_acerto_estoura_o_teto()
    {
        var juggle = new AerialJuggleState();
        for (var i = 0; i < AerialJuggleState.LimiteDeAcertos; i++)
            juggle.RegistrarAcerto();

        Assert.False(juggle.RegistrarAcerto());
    }

    [Fact]
    public void Acertos_alem_do_teto_continuam_estourados()
    {
        var juggle = new AerialJuggleState();
        for (var i = 0; i < AerialJuggleState.LimiteDeAcertos + 3; i++)
            juggle.RegistrarAcerto();

        Assert.False(juggle.RegistrarAcerto());
    }

    [Fact]
    public void Reset_devolve_ao_estado_de_recem_criado()
    {
        var juggle = new AerialJuggleState();
        for (var i = 0; i < AerialJuggleState.LimiteDeAcertos; i++)
            juggle.RegistrarAcerto();

        juggle.Reset();

        Assert.Equal(0, juggle.Acertos);
        Assert.True(juggle.RegistrarAcerto());
        Assert.Equal(1, juggle.Acertos);
    }
}
