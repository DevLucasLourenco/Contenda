using Contenda.GameModes.Horde;
using Xunit;

namespace Contenda.Tests;

/// <summary>Escolha de ponto de spawn — ticket 27, spec 10 §6.</summary>
public sealed class SpawnPointMathTests
{
    [Theory]
    [InlineData(12f, 12f, 30f, true)]
    [InlineData(30f, 12f, 30f, true)]
    [InlineData(20f, 12f, 30f, true)]
    [InlineData(11.9f, 12f, 30f, false)]
    [InlineData(30.1f, 12f, 30f, false)]
    public void IsDistanceValid_respeita_os_limites_inclusive(float distancia, float min, float max, bool esperado)
    {
        Assert.Equal(esperado, SpawnPointMath.IsDistanceValid(distancia, min, max));
    }

    [Fact]
    public void ChooseWeightedIndex_pesos_iguais_sorteio_no_meio_escolhe_o_do_meio()
    {
        var pesos = new[] { 1f, 1f, 1f };

        // total = 3; sorteio 0,5 -> alvo 1,5 -> cai no segundo candidato (índice 1).
        var indice = SpawnPointMath.ChooseWeightedIndex(pesos, sorteio01: 0.5f);

        Assert.Equal(1, indice);
    }

    [Fact]
    public void ChooseWeightedIndex_sorteio_zero_escolhe_o_primeiro()
    {
        var pesos = new[] { 1f, 1f, 1f };

        Assert.Equal(0, SpawnPointMath.ChooseWeightedIndex(pesos, sorteio01: 0f));
    }

    [Fact]
    public void ChooseWeightedIndex_peso_menor_reduz_a_chance_proporcionalmente()
    {
        // Pesos [1, 1, 0.2]: total 2,2. O terceiro só é escolhido para
        // sorteio >= 2,0/2,2 ≈ 0,909 -- bem menos espaço que os outros dois.
        var pesos = new[] { 1f, 1f, 0.2f };

        Assert.Equal(2, SpawnPointMath.ChooseWeightedIndex(pesos, sorteio01: 0.95f));
        Assert.NotEqual(2, SpawnPointMath.ChooseWeightedIndex(pesos, sorteio01: 0.5f));
    }

    [Fact]
    public void ChooseWeightedIndex_sorteio_quase_um_nao_estoura_o_indice()
    {
        var pesos = new[] { 1f, 1f };

        var indice = SpawnPointMath.ChooseWeightedIndex(pesos, sorteio01: 0.999999f);

        Assert.Equal(1, indice);
    }

    [Fact]
    public void ChooseWeightedIndex_um_candidato_so_sempre_escolhe_ele()
    {
        var pesos = new[] { 1f };

        Assert.Equal(0, SpawnPointMath.ChooseWeightedIndex(pesos, sorteio01: 0.7f));
    }

    [Fact]
    public void ChooseWeightedIndex_pesos_todos_zero_nao_quebra()
    {
        var pesos = new[] { 0f, 0f, 0f };

        Assert.Equal(0, SpawnPointMath.ChooseWeightedIndex(pesos, sorteio01: 0.5f));
    }
}
