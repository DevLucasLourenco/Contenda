using Contenda.GameModes.Horde;
using Xunit;

namespace Contenda.Tests;

public sealed class ScoreKeeperTests
{
    private static readonly ScoreRules Regras = new(
        ComboWindowSeconds: 3f, ComboStep: 0.1f, ComboCap: 3f, WaveStep: 0.1f, FlawlessWaveBonus: 250);

    private static ScoreKeeper Novo() => new(Regras);

    [Fact]
    public void PrimeiroAbateValeOValorBase()
    {
        var placar = Novo();

        Assert.Equal(10, placar.RegisterKill(now: 1f, baseValue: 10, waveIndex: 0));
        Assert.Equal(10, placar.Score);
    }

    [Fact]
    public void OndaMaisAvancadaValeMais()
    {
        var placar = Novo();

        Assert.Equal(13, placar.RegisterKill(1f, 10, waveIndex: 3));
    }

    [Fact]
    public void AbateEmSequenciaRapidaMultiplica()
    {
        var placar = Novo();
        placar.RegisterKill(1f, 10, 0);

        Assert.Equal(11, placar.RegisterKill(2f, 10, 0));
        Assert.Equal(12, placar.RegisterKill(3f, 10, 0));
        Assert.Equal(1.2f, placar.ComboMultiplier, 3);
    }

    [Fact]
    public void AbateDepoisDaJanelaRecomecaOCombo()
    {
        var placar = Novo();
        placar.RegisterKill(1f, 10, 0);
        placar.RegisterKill(2f, 10, 0);

        Assert.Equal(10, placar.RegisterKill(5.5f, 10, 0));
        Assert.Equal(1f, placar.ComboMultiplier);
    }

    [Fact]
    public void ComboTemTeto()
    {
        var placar = Novo();

        for (var i = 0; i < 60; i++)
            placar.RegisterKill(i * 0.5f, 10, 0);

        Assert.Equal(3f, placar.ComboMultiplier, 3);
    }

    [Fact]
    public void ApanharZeraOMultiplicador()
    {
        var placar = Novo();
        placar.RegisterKill(1f, 10, 0);
        placar.RegisterKill(2f, 10, 0);

        placar.RegisterPlayerDamaged();

        Assert.Equal(1f, placar.ComboMultiplier);
        Assert.Equal(10, placar.RegisterKill(2.5f, 10, 0));
    }

    [Fact]
    public void LimparOndaSemApanharDaBonus()
    {
        var placar = Novo();
        placar.StartWave();

        Assert.Equal(250, placar.ClearWave());
        Assert.Equal(250, placar.Score);
    }

    [Fact]
    public void LimparOndaApanhandoNaoDaBonus()
    {
        var placar = Novo();
        placar.StartWave();
        placar.RegisterPlayerDamaged();

        Assert.Equal(0, placar.ClearWave());
    }

    [Fact]
    public void DanoDeUmaOndaNaoContaNaSeguinte()
    {
        var placar = Novo();
        placar.StartWave();
        placar.RegisterPlayerDamaged();
        placar.ClearWave();

        placar.StartWave();

        Assert.Equal(250, placar.ClearWave());
    }

    [Fact]
    public void ComboExpiraSozinhoSemAbates()
    {
        var placar = Novo();
        placar.RegisterKill(1f, 10, 0);
        placar.RegisterKill(2f, 10, 0);

        Assert.False(placar.ExpireCombo(4f));
        Assert.True(placar.ExpireCombo(5.5f));
        Assert.Equal(1f, placar.ComboMultiplier);
    }

    [Fact]
    public void ExpirarComboJaZeradoNaoAvisaDeNovo()
    {
        var placar = Novo();
        placar.RegisterKill(1f, 10, 0);

        Assert.False(placar.ExpireCombo(100f));
    }

    [Fact]
    public void AbateExatamenteNoLimiteDaJanelaAindaEncadeia()
    {
        var placar = Novo();
        placar.RegisterKill(1f, 10, 0);

        Assert.Equal(11, placar.RegisterKill(4f, 10, 0));
    }

    [Fact]
    public void AbateUmPoucoDepoisDoLimiteDaJanelaQuebraASequencia()
    {
        var placar = Novo();
        placar.RegisterKill(1f, 10, 0);

        Assert.Equal(10, placar.RegisterKill(4.01f, 10, 0));
    }

    [Fact]
    public void OsNumerosVemDasRegrasNaoDeConstantes()
    {
        var placar = new ScoreKeeper(new ScoreRules(
            ComboWindowSeconds: 1f, ComboStep: 0.5f, ComboCap: 2f, WaveStep: 1f, FlawlessWaveBonus: 40));
        placar.StartWave();

        placar.RegisterKill(0f, 10, waveIndex: 1);
        var segundo = placar.RegisterKill(0.5f, 10, waveIndex: 1);

        Assert.Equal(30, segundo);
        Assert.Equal(40, placar.ClearWave());
        Assert.False(placar.ExpireCombo(1f));
        Assert.True(placar.ExpireCombo(2f));
    }
}
