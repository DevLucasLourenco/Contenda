using Contenda.Components.Targeting;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Projeção do cursor no mundo.
/// </summary>
/// <remarks>
/// O plano de mira fica na **altura do torso**, não em Y=0. Mirar no chão faz o
/// erro crescer com a distância: quanto mais longe o cursor, mais o ponto
/// projetado se afasta de onde o jogador acha que está apontando.
/// </remarks>
public sealed class TargetingMathTests
{
    private const float Tol = 0.001f;

    [Fact]
    public void Raio_apontando_para_baixo_encontra_o_plano()
    {
        var origem = new Vector3(0f, 10f, 0f);
        var direcao = Vector3.Down;

        var achou = TargetingMath.TryProjectToPlane(origem, direcao, 1f, out var ponto);

        Assert.True(achou);
        Assert.Equal(1f, ponto.Y, Tol);
        Assert.Equal(0f, ponto.X, Tol);
        Assert.Equal(0f, ponto.Z, Tol);
    }

    [Fact]
    public void Raio_inclinado_encontra_o_plano_no_ponto_certo()
    {
        // De 10 m de altura, descendo 45°: percorre 9 m de altura e 9 m no plano.
        var origem = new Vector3(0f, 10f, 0f);
        var direcao = new Vector3(1f, -1f, 0f).Normalized();

        Assert.True(TargetingMath.TryProjectToPlane(origem, direcao, 1f, out var ponto));
        Assert.Equal(9f, ponto.X, Tol);
        Assert.Equal(1f, ponto.Y, Tol);
    }

    [Fact]
    public void Raio_paralelo_ao_plano_nao_encontra_nada()
    {
        var achou = TargetingMath.TryProjectToPlane(
            new Vector3(0f, 10f, 0f), Vector3.Right, 1f, out _);

        Assert.False(achou);
    }

    [Fact]
    public void Raio_afastando_se_do_plano_nao_encontra_nada()
    {
        // Apontar para cima, com o plano abaixo, é a mira além do horizonte —
        // acontece de verdade quando o cursor vai para o topo da tela.
        var achou = TargetingMath.TryProjectToPlane(
            new Vector3(0f, 10f, 0f), Vector3.Up, 1f, out _);

        Assert.False(achou);
    }

    [Fact]
    public void Origem_exatamente_sobre_o_plano_resolve_na_propria_origem()
    {
        Assert.True(TargetingMath.TryProjectToPlane(
            new Vector3(3f, 1f, 4f), Vector3.Down, 1f, out var ponto));
        Assert.Equal(new Vector3(3f, 1f, 4f), ponto);
    }

    [Fact]
    public void A_direcao_de_mira_ignora_a_altura()
    {
        // O personagem gira só no plano: um alvo acima ou abaixo não pode
        // fazê-lo inclinar.
        var de = new Vector3(0f, 1f, 0f);
        var para = new Vector3(10f, 7f, 0f);

        var direcao = TargetingMath.AimDirection(de, para);

        Assert.Equal(0f, direcao.Y, Tol);
        Assert.Equal(1f, direcao.Length(), Tol);
        Assert.Equal(1f, direcao.X, Tol);
    }

    [Fact]
    public void Mira_sobre_o_proprio_personagem_nao_produz_direcao_espuria()
    {
        var direcao = TargetingMath.AimDirection(
            new Vector3(5f, 1f, 5f), new Vector3(5f, 1f, 5f));

        Assert.Equal(Vector3.Zero, direcao);
    }
}
