using Contenda.Vfx;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// A subida e o desaparecimento de um número de dano flutuante — ticket 11.
/// </summary>
public sealed class FloatingDamageMathTests
{
    private const float Tol = 0.001f;
    private const float Lifetime = 0.6f;
    private const float Rise = 1f;

    [Fact]
    public void No_instante_zero_nao_subiu_e_esta_totalmente_opaco()
    {
        var ativo = FloatingDamageMath.Evaluate(0f, Lifetime, Rise, out var altura, out var alfa);

        Assert.True(ativo);
        Assert.Equal(0f, altura, Tol);
        Assert.Equal(1f, alfa, Tol);
    }

    [Fact]
    public void Na_metade_do_tempo_subiu_e_esta_meio_transparente()
    {
        var ativo = FloatingDamageMath.Evaluate(Lifetime * 0.5f, Lifetime, Rise, out var altura, out var alfa);

        Assert.True(ativo);
        Assert.Equal(Rise * 0.5f, altura, Tol);
        Assert.Equal(0.5f, alfa, Tol);
    }

    [Fact]
    public void No_fim_do_tempo_esta_totalmente_transparente_e_inativo()
    {
        var ativo = FloatingDamageMath.Evaluate(Lifetime, Lifetime, Rise, out var altura, out var alfa);

        Assert.False(ativo);
        Assert.Equal(0f, alfa, Tol);
    }

    [Fact]
    public void Depois_do_fim_continua_inativo()
    {
        var ativo = FloatingDamageMath.Evaluate(Lifetime * 5f, Lifetime, Rise, out _, out var alfa);

        Assert.False(ativo);
        Assert.Equal(0f, alfa, Tol);
    }

    [Fact]
    public void Duracao_zero_e_inativo_imediatamente()
    {
        var ativo = FloatingDamageMath.Evaluate(0f, 0f, Rise, out _, out var alfa);

        Assert.False(ativo);
        Assert.Equal(0f, alfa, Tol);
    }
}
