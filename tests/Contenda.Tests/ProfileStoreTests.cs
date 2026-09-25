using System;
using System.IO;
using Contenda.Persistence;
using Xunit;

namespace Contenda.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "contenda-profile-" + Guid.NewGuid().ToString("N"));

    private string Caminho => Path.Combine(_pasta, "profile.cfg");

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
            Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public void ArquivoInexistenteViraPerfilVazio()
    {
        var perfil = new ProfileStore(Caminho).Load();

        Assert.Equal(0, perfil.TotalMatches);
        Assert.Empty(perfil.Best);
    }

    [Fact]
    public void RegistrarPartidaSomaEstatisticasEGuardaOMelhor()
    {
        new ProfileStore(Caminho).RecordMatch("swordsman", score: 1200, waves: 3, kills: 30, timeSeconds: 200);

        var perfil = new ProfileStore(Caminho).Load();
        Assert.Equal(1, perfil.TotalMatches);
        Assert.Equal(30, perfil.TotalKills);
        Assert.Equal(200, perfil.TotalPlaytimeSeconds);
        Assert.Equal(new BestResult(1200, 3, 200), perfil.Best["swordsman"]);
    }

    [Fact]
    public void PartidaPiorNaoSubstituiORecordeMasSomaAsEstatisticas()
    {
        var loja = new ProfileStore(Caminho);
        loja.RecordMatch("swordsman", 5000, 5, 60, 500);

        loja.RecordMatch("swordsman", 900, 2, 12, 100);

        var perfil = loja.Load();
        Assert.Equal(new BestResult(5000, 5, 500), perfil.Best["swordsman"]);
        Assert.Equal(2, perfil.TotalMatches);
        Assert.Equal(72, perfil.TotalKills);
    }

    [Fact]
    public void PartidaMelhorSubstituiORecorde()
    {
        var loja = new ProfileStore(Caminho);
        loja.RecordMatch("swordsman", 900, 2, 12, 100);

        loja.RecordMatch("swordsman", 5000, 5, 60, 500);

        Assert.Equal(new BestResult(5000, 5, 500), loja.Load().Best["swordsman"]);
    }

    [Fact]
    public void CadaPersonagemTemOSeuRecorde()
    {
        var loja = new ProfileStore(Caminho);

        loja.RecordMatch("swordsman", 1000, 3, 20, 100);
        loja.RecordMatch("gunslinger", 700, 2, 10, 90);

        var perfil = loja.Load();
        Assert.Equal(1000, perfil.Best["swordsman"].Score);
        Assert.Equal(700, perfil.Best["gunslinger"].Score);
    }

    [Fact]
    public void ArquivoCorrompidoNaoDerrubaOCarregamento()
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, "\0\0isso nao e um perfil [[[ = = =");

        var perfil = new ProfileStore(Caminho).Load();

        Assert.Equal(0, perfil.TotalMatches);
    }

    [Fact]
    public void ArquivoCorrompidoFicaGuardadoAntesDeSerSubstituido()
    {
        Directory.CreateDirectory(_pasta);
        File.WriteAllText(Caminho, "lixo que nao e um perfil");

        new ProfileStore(Caminho).RecordMatch("swordsman", 100, 1, 5, 30);

        Assert.Equal("lixo que nao e um perfil", File.ReadAllText(Caminho + ".corrupt"));
        Assert.Equal(1, new ProfileStore(Caminho).Load().TotalMatches);
    }

    [Fact]
    public void PerfilValidoNaoGeraCopiaCorrompida()
    {
        var loja = new ProfileStore(Caminho);
        loja.RecordMatch("swordsman", 100, 1, 5, 30);

        loja.RecordMatch("swordsman", 200, 2, 6, 40);

        Assert.False(File.Exists(Caminho + ".corrupt"));
    }

    [Fact]
    public void GravacaoInterrompidaNoMeioNaoCorrompeOArquivoAnterior()
    {
        var loja = new ProfileStore(Caminho);
        loja.RecordMatch("swordsman", 5000, 5, 60, 500);
        var antes = File.ReadAllText(Caminho);

        var quebrada = new ProfileStore(Caminho, escrever: (caminho, _) =>
        {
            File.WriteAllText(caminho, "[stats]\ntotal_ma");
            throw new IOException("o jogo fechou no meio");
        });

        Assert.Throws<IOException>(() => quebrada.RecordMatch("swordsman", 9999, 5, 99, 400));

        Assert.Equal(antes, File.ReadAllText(Caminho));
        Assert.Equal(5000, loja.Load().Best["swordsman"].Score);
    }

    [Fact]
    public void RestoDeGravacaoAnteriorNaoAtrapalhaAProxima()
    {
        var loja = new ProfileStore(Caminho);
        loja.RecordMatch("swordsman", 1000, 3, 20, 100);
        File.WriteAllText(Caminho + ".tmp", "lixo de uma gravacao que nao terminou");

        loja.RecordMatch("swordsman", 2000, 4, 25, 150);

        Assert.Equal(2000, loja.Load().Best["swordsman"].Score);
        Assert.False(File.Exists(Caminho + ".tmp"));
    }

    [Fact]
    public void ArquivoGravadoTemOFormatoDaSpec()
    {
        new ProfileStore(Caminho).RecordMatch("swordsman", 18400, 5, 843, 512);

        var texto = File.ReadAllText(Caminho).Replace(" ", "");

        Assert.Contains("[stats]", texto);
        Assert.Contains("total_matches=1", texto);
        Assert.Contains("[best.swordsman]", texto);
        Assert.Contains("score=18400", texto);
        Assert.Contains("time_seconds=512", texto);
    }
}
