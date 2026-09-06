using Contenda.Components.Stats;
using Xunit;

namespace Contenda.Tests;

/// <summary>Sorteio e aplicação do crítico — ticket 18, spec 16 §5.</summary>
public sealed class CritMathTests
{
    [Fact]
    public void Sorteio_abaixo_da_chance_e_critico()
    {
        Assert.True(CritMath.Rolar(chance: 0.5f, sorteio01: 0.1f));
    }

    [Fact]
    public void Sorteio_acima_da_chance_nao_e_critico()
    {
        Assert.False(CritMath.Rolar(chance: 0.5f, sorteio01: 0.9f));
    }

    [Fact]
    public void Sorteio_igual_a_chance_nao_e_critico()
    {
        // O intervalo é [0, chance) -- na fronteira exata, não conta.
        Assert.False(CritMath.Rolar(chance: 0.5f, sorteio01: 0.5f));
    }

    [Fact]
    public void Chance_zero_nunca_e_critico_mesmo_com_sorteio_zero()
    {
        Assert.False(CritMath.Rolar(chance: 0f, sorteio01: 0f));
    }

    [Fact]
    public void Chance_um_e_sempre_critico_mesmo_com_sorteio_quase_maximo()
    {
        Assert.True(CritMath.Rolar(chance: 1f, sorteio01: 0.999999f));
    }

    [Fact]
    public void Chance_negativa_nunca_e_critico()
    {
        // Defensivo: nada no projeto define CritChance negativo hoje, mas um
        // modificador futuro que zere demais não deveria virar "sempre crítico".
        Assert.False(CritMath.Rolar(chance: -0.2f, sorteio01: 0f));
    }

    [Fact]
    public void Aplicar_multiplica_quando_critico()
    {
        Assert.Equal(20f, CritMath.Aplicar(dano: 10f, critico: true, multiplicador: 2f));
    }

    [Fact]
    public void Aplicar_nao_multiplica_quando_nao_critico()
    {
        Assert.Equal(10f, CritMath.Aplicar(dano: 10f, critico: false, multiplicador: 2f));
    }
}
