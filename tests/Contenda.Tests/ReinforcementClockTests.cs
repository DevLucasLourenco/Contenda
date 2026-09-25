using Contenda.GameModes.Horde;
using Xunit;

namespace Contenda.Tests;

public sealed class ReinforcementClockTests
{
    [Fact]
    public void IntervaloZeroNuncaVenceReforco()
    {
        var relogio = new ReinforcementClock(0f);

        Assert.False(relogio.Tick(100f, active: true));
    }

    [Fact]
    public void NaoVenceAntesDoIntervalo()
    {
        var relogio = new ReinforcementClock(5f);

        Assert.False(relogio.Tick(2f, active: true));
        Assert.False(relogio.Tick(2.9f, active: true));
    }

    [Fact]
    public void VenceAoCompletarOIntervalo()
    {
        var relogio = new ReinforcementClock(5f);

        relogio.Tick(3f, active: true);

        Assert.True(relogio.Tick(2f, active: true));
    }

    [Fact]
    public void ContinuaVencidoAteSerConsumido()
    {
        var relogio = new ReinforcementClock(1f);

        relogio.Tick(1f, active: true);

        Assert.True(relogio.Tick(0.1f, active: true));
        Assert.True(relogio.Tick(0.1f, active: true));
    }

    [Fact]
    public void ConsumirReiniciaAContagem()
    {
        var relogio = new ReinforcementClock(1f);
        relogio.Tick(1f, active: true);

        relogio.Consume();

        Assert.False(relogio.Tick(0.5f, active: true));
        Assert.True(relogio.Tick(0.5f, active: true));
    }

    [Fact]
    public void InativoNaoAcumulaTempo()
    {
        var relogio = new ReinforcementClock(1f);

        Assert.False(relogio.Tick(10f, active: false));
        Assert.False(relogio.Tick(0.5f, active: true));
    }

    [Fact]
    public void InativoNaoZeraOQueJaAcumulou()
    {
        var relogio = new ReinforcementClock(1f);
        relogio.Tick(0.6f, active: true);

        relogio.Tick(5f, active: false);

        Assert.True(relogio.Tick(0.4f, active: true));
    }

    [Fact]
    public void LoteSemEspacoSobOTetoEsperaSemSerDescartado()
    {
        var relogio = new ReinforcementClock(1f);

        Assert.False(relogio.TryDeliver(1f, active: true, activeCount: 11, batch: 2, cap: 12));
        Assert.True(relogio.TryDeliver(0.1f, active: true, activeCount: 10, batch: 2, cap: 12));
    }

    [Fact]
    public void LoteEntregueReiniciaAContagem()
    {
        var relogio = new ReinforcementClock(1f);
        relogio.TryDeliver(1f, active: true, activeCount: 0, batch: 2, cap: 12);

        Assert.False(relogio.TryDeliver(0.5f, active: true, activeCount: 0, batch: 2, cap: 12));
    }

    [Fact]
    public void ChefeInativoNaoEntregaMesmoComEspaco()
    {
        var relogio = new ReinforcementClock(1f);

        Assert.False(relogio.TryDeliver(5f, active: false, activeCount: 0, batch: 2, cap: 12));
    }
}
