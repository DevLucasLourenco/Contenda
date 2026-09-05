using Contenda.Weapons;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Menor distância entre um alvo e o segmento de um tiro.
/// </summary>
public sealed class HitscanMathTests
{
    private const float Tol = 0.001f;

    [Fact]
    public void Alvo_bem_na_frente_e_atingido()
    {
        var atingiu = HitscanMath.TryHitSegment(
            origem: Vector3.Zero,
            direcao: Vector3.Forward,
            comprimento: 10f,
            alvo: new Vector3(0f, 0f, -5f),
            raioDeAcerto: 0.5f,
            distanciaAoLongoDoRaio: out var t);

        Assert.True(atingiu);
        Assert.Equal(5f, t, Tol);
    }

    [Fact]
    public void Alvo_fora_do_raio_lateral_nao_e_atingido()
    {
        var atingiu = HitscanMath.TryHitSegment(
            Vector3.Zero, Vector3.Forward, 10f,
            alvo: new Vector3(2f, 0f, -5f),
            raioDeAcerto: 0.5f,
            distanciaAoLongoDoRaio: out _);

        Assert.False(atingiu);
    }

    [Fact]
    public void Alvo_dentro_do_raio_lateral_e_atingido()
    {
        var atingiu = HitscanMath.TryHitSegment(
            Vector3.Zero, Vector3.Forward, 10f,
            alvo: new Vector3(0.3f, 0f, -5f),
            raioDeAcerto: 0.5f,
            distanciaAoLongoDoRaio: out _);

        Assert.True(atingiu);
    }

    [Fact]
    public void Alvo_alem_do_alcance_nao_e_atingido()
    {
        var atingiu = HitscanMath.TryHitSegment(
            Vector3.Zero, Vector3.Forward, 10f,
            alvo: new Vector3(0f, 0f, -15f),
            raioDeAcerto: 0.5f,
            distanciaAoLongoDoRaio: out var t);

        Assert.False(atingiu);
        // O ponto mais próximo do segmento fica no fim dele, não no alvo --
        // a distância lateral daí até o alvo é maior que o raio.
        Assert.Equal(10f, t, Tol);
    }

    [Fact]
    public void Alvo_atras_da_origem_nao_e_atingido()
    {
        var atingiu = HitscanMath.TryHitSegment(
            Vector3.Zero, Vector3.Forward, 10f,
            alvo: new Vector3(0f, 0f, 5f),
            raioDeAcerto: 0.5f,
            distanciaAoLongoDoRaio: out var t);

        Assert.False(atingiu);
        Assert.Equal(0f, t, Tol);
    }

    [Fact]
    public void Alvo_exatamente_na_borda_do_raio_e_atingido()
    {
        var atingiu = HitscanMath.TryHitSegment(
            Vector3.Zero, Vector3.Forward, 10f,
            alvo: new Vector3(0.5f, 0f, -5f),
            raioDeAcerto: 0.5f,
            distanciaAoLongoDoRaio: out _);

        Assert.True(atingiu);
    }

    [Fact]
    public void Alvo_na_origem_da_origem_conta_como_distancia_zero()
    {
        var atingiu = HitscanMath.TryHitSegment(
            Vector3.Zero, Vector3.Forward, 10f,
            alvo: Vector3.Zero,
            raioDeAcerto: 0.5f,
            distanciaAoLongoDoRaio: out var t);

        Assert.True(atingiu);
        Assert.Equal(0f, t, Tol);
    }
}
