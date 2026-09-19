using Contenda.Vfx;
using Xunit;

namespace Contenda.Tests;

/// <summary>Marcador de nascimento no chão — ticket 27, spec 10 §6.</summary>
public sealed class SpawnMarkerMathTests
{
    [Fact]
    public void Comeca_pequeno()
    {
        SpawnMarkerMath.Evaluate(0f, 0.4f, out var escala, out _);

        Assert.Equal(0.3f, escala, precision: 4);
    }

    [Fact]
    public void Cresce_ate_o_tamanho_cheio_perto_do_fim()
    {
        SpawnMarkerMath.Evaluate(0.39f, 0.4f, out var escala, out _);

        Assert.True(escala > 0.9f);
    }

    [Fact]
    public void Fica_opaco_na_maior_parte_do_tempo()
    {
        SpawnMarkerMath.Evaluate(0.2f, 0.4f, out _, out var alfa);

        Assert.Equal(1f, alfa);
    }

    [Fact]
    public void Comeca_a_sumir_soh_perto_do_fim()
    {
        SpawnMarkerMath.Evaluate(0.39f, 0.4f, out _, out var alfa);

        Assert.True(alfa < 1f && alfa > 0f);
    }

    [Fact]
    public void Depois_da_duracao_nao_esta_mais_ativo()
    {
        var ativo = SpawnMarkerMath.Evaluate(0.4f, 0.4f, out _, out var alfa);

        Assert.False(ativo);
        Assert.Equal(0f, alfa);
    }

    [Fact]
    public void Duracao_zero_nao_quebra()
    {
        var ativo = SpawnMarkerMath.Evaluate(0f, 0f, out _, out var alfa);

        Assert.False(ativo);
        Assert.Equal(0f, alfa);
    }
}
