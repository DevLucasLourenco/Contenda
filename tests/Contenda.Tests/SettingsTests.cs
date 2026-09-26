using System.Collections.Generic;
using Contenda.Settings;
using Xunit;

namespace Contenda.Tests;

public sealed class SettingsSerializerTests
{
    [Fact]
    public void TextoVazioDaOsPadroes()
    {
        var s = SettingsSerializer.Parse("");

        Assert.Equal(WindowModeOption.Windowed, s.Video.WindowMode);
        Assert.Equal(100, s.Audio.Master);
        Assert.Equal(100, s.Gameplay.ShakeIntensityPercent);
        Assert.True(s.Gameplay.ShowDamageNumbers);
        Assert.Equal(CommandWindowOption.Normal, s.Gameplay.CommandWindow);
        Assert.Empty(s.Bindings);
    }

    [Fact]
    public void IdaEVoltaPreservaTudo()
    {
        var original = new GameSettings();
        original.Video.WindowMode = WindowModeOption.BorderlessFullscreen;
        original.Video.ResolutionWidth = 1600;
        original.Video.ResolutionHeight = 900;
        original.Video.VSync = VSyncOption.Adaptive;
        original.Video.FpsLimit = 144;
        original.Video.Shadows = ShadowQualityOption.Low;
        original.Video.RenderScale = 0.75f;
        original.Audio.Master = 80;
        original.Audio.Music = 30;
        original.Audio.Sfx = 55;
        original.Audio.Ui = 10;
        original.Audio.Ambience = 0;
        original.Gameplay.ShakeIntensityPercent = 40;
        original.Gameplay.ShowDamageNumbers = false;
        original.Gameplay.ShowComboGuide = false;
        original.Gameplay.CommandWindow = CommandWindowOption.Long;
        original.Bindings["dash"] = [BindingSpec.Key(69)];
        original.Bindings["attack_basic"] = [BindingSpec.Mouse(2), BindingSpec.Key(70)];

        var lido = SettingsSerializer.Parse(SettingsSerializer.Serialize(original));

        Assert.Equal(original.Video.WindowMode, lido.Video.WindowMode);
        Assert.Equal(1600, lido.Video.ResolutionWidth);
        Assert.Equal(900, lido.Video.ResolutionHeight);
        Assert.Equal(VSyncOption.Adaptive, lido.Video.VSync);
        Assert.Equal(144, lido.Video.FpsLimit);
        Assert.Equal(ShadowQualityOption.Low, lido.Video.Shadows);
        Assert.Equal(0.75f, lido.Video.RenderScale, 3);
        Assert.Equal(80, lido.Audio.Master);
        Assert.Equal(30, lido.Audio.Music);
        Assert.Equal(55, lido.Audio.Sfx);
        Assert.Equal(10, lido.Audio.Ui);
        Assert.Equal(0, lido.Audio.Ambience);
        Assert.Equal(40, lido.Gameplay.ShakeIntensityPercent);
        Assert.False(lido.Gameplay.ShowDamageNumbers);
        Assert.False(lido.Gameplay.ShowComboGuide);
        Assert.Equal(CommandWindowOption.Long, lido.Gameplay.CommandWindow);
        Assert.Equal(["key:69"], lido.Bindings["dash"]);
        Assert.Equal(["mouse:2", "key:70"], lido.Bindings["attack_basic"]);
    }

    [Fact]
    public void VersaoDesconhecidaCaiNosPadroesEmVezDeQuebrar()
    {
        var s = SettingsSerializer.Parse("[meta]\nversion = 99\n[audio]\nmaster = 5\n");

        Assert.Equal(100, s.Audio.Master);
    }

    [Fact]
    public void LixoNaoDerrubaAsLinhasBoas()
    {
        var s = SettingsSerializer.Parse("\0\0[[[ lixo\n[audio]\nmaster = abc\nmusic = 40\n=== \n");

        Assert.Equal(100, s.Audio.Master);
        Assert.Equal(40, s.Audio.Music);
    }

    [Fact]
    public void NumerosForaDaFaixaSaoPuxadosParaDentro()
    {
        var s = SettingsSerializer.Parse(
            "[audio]\nmaster = 900\nmusic = -5\n[gameplay]\nshake_intensity = 999\n[video]\nrender_scale = 7\nfps_limit = -3\n");

        Assert.Equal(100, s.Audio.Master);
        Assert.Equal(0, s.Audio.Music);
        Assert.Equal(150, s.Gameplay.ShakeIntensityPercent);
        Assert.Equal(1f, s.Video.RenderScale);
        Assert.Equal(0, s.Video.FpsLimit);
    }

    [Fact]
    public void EnumeracaoInvalidaFicaNoPadrao()
    {
        var s = SettingsSerializer.Parse("[video]\nwindow_mode = disco_voador\nvsync = adaptive\n");

        Assert.Equal(WindowModeOption.Windowed, s.Video.WindowMode);
        Assert.Equal(VSyncOption.Adaptive, s.Video.VSync);
    }

    [Fact]
    public void TeclaInvalidaEhIgnoradaMasAcaoSemTeclaContinuaSemTecla()
    {
        var s = SettingsSerializer.Parse("[bindings]\ndash = key:69, banana, mouse:0\njump =\n");

        Assert.Equal(["key:69"], s.Bindings["dash"]);
        Assert.Empty(s.Bindings["jump"]);
    }

    [Theory]
    [InlineData(CommandWindowOption.Short, 0.5f)]
    [InlineData(CommandWindowOption.Normal, 0.7f)]
    [InlineData(CommandWindowOption.Long, 0.9f)]
    public void JanelaDeComandosTemOsTemposDaSpec(CommandWindowOption opcao, float segundos)
    {
        var jogo = new GameplaySettings { CommandWindow = opcao };

        Assert.Equal(segundos, jogo.CommandWindowSeconds);
    }

    [Fact]
    public void CloneEhIndependenteDoOriginal()
    {
        var original = new GameSettings();
        original.Bindings["dash"] = [BindingSpec.Key(69)];

        var copia = original.Clone();
        copia.Audio.Master = 3;
        copia.Bindings["dash"].Add(BindingSpec.Key(70));

        Assert.Equal(100, original.Audio.Master);
        Assert.Single(original.Bindings["dash"]);
    }
}

public sealed class BindingSpecTests
{
    private static Dictionary<string, List<string>> Padrao() => new()
    {
        ["dash"] = ["key:32"],
        ["jump"] = ["key:4194326"],
        ["attack_basic"] = ["mouse:1"],
    };

    [Theory]
    [InlineData("key:87", true)]
    [InlineData("mouse:2", true)]
    [InlineData("key:0", false)]
    [InlineData("joy:1", false)]
    [InlineData("key:", false)]
    [InlineData("lixo", false)]
    public void ValidaAForma(string spec, bool valido) => Assert.Equal(valido, BindingSpec.IsValid(spec));

    [Fact]
    public void TrocaDoJogadorSubstituiOPadraoDaAcaoSemSomar()
    {
        var efetivo = BindingSpec.Effective(Padrao(), new Dictionary<string, List<string>> { ["dash"] = ["key:69"] });

        Assert.Equal(["key:69"], efetivo["dash"]);
        Assert.Equal(["key:4194326"], efetivo["jump"]);
    }

    [Fact]
    public void ConflitoApontaAOutraAcaoQueUsaATecla()
    {
        Assert.Equal("jump", BindingSpec.FindConflict(Padrao(), "dash", "key:4194326"));
    }

    [Fact]
    public void AMesmaAcaoNaoConflitaConsigo()
    {
        Assert.Null(BindingSpec.FindConflict(Padrao(), "dash", "key:32"));
    }

    [Fact]
    public void TeclaLivreNaoTemConflito()
    {
        Assert.Null(BindingSpec.FindConflict(Padrao(), "dash", "key:69"));
    }

    [Fact]
    public void RebindComConflitoTiraATeclaDeQuemUsavaEDevolveQuem()
    {
        var efetivo = Padrao();

        var perdeu = BindingSpec.Rebind(efetivo, "dash", "key:4194326");

        Assert.Equal("jump", perdeu);
        Assert.Equal(["key:4194326"], efetivo["dash"]);
        Assert.Empty(efetivo["jump"]);
    }

    [Fact]
    public void RebindSemConflitoSoTrocaAAcao()
    {
        var efetivo = Padrao();

        Assert.Null(BindingSpec.Rebind(efetivo, "dash", "key:69"));
        Assert.Equal(["key:69"], efetivo["dash"]);
        Assert.Equal(["key:4194326"], efetivo["jump"]);
    }
}
