using Contenda.Weapons;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// O avanço do golpe, distribuído pelo tempo de preparação.
/// </summary>
public sealed class LungeMotionTests
{
    private const float Tol = 0.001f;
    private const float Passo = 1f / 60f;

    private static float Percorrer(LungeMotion avanco, float segundos, float passo = Passo)
    {
        var total = 0f;
        for (var t = 0f; t < segundos; t += passo)
            total += avanco.Consume(passo);
        return total;
    }

    [Fact]
    public void Percorre_exatamente_a_distancia_pedida()
    {
        var avanco = new LungeMotion();
        avanco.Start(0.6f, 0.18f);

        Assert.Equal(0.6f, Percorrer(avanco, 0.5f), Tol);
        Assert.False(avanco.IsMoving);
    }

    [Fact]
    public void Nunca_ultrapassa_a_distancia_pedida()
    {
        var avanco = new LungeMotion();
        avanco.Start(0.6f, 0.18f);

        var total = 0f;
        for (var i = 0; i < 200; i++)
        {
            total += avanco.Consume(Passo);
            Assert.True(total <= 0.6f + Tol, $"ultrapassou no passo {i}: {total}");
        }
    }

    [Fact]
    public void O_resultado_nao_depende_do_framerate()
    {
        var a30 = new LungeMotion(); a30.Start(1f, 0.18f);
        var a60 = new LungeMotion(); a60.Start(1f, 0.18f);
        var a144 = new LungeMotion(); a144.Start(1f, 0.18f);

        Assert.Equal(1f, Percorrer(a30, 0.5f, 1f / 30f), Tol);
        Assert.Equal(1f, Percorrer(a60, 0.5f, 1f / 60f), Tol);
        Assert.Equal(1f, Percorrer(a144, 0.5f, 1f / 144f), Tol);
    }

    [Fact]
    public void Leva_mais_de_um_quadro_e_o_ponto_do_exercicio()
    {
        // Se o avanço couber num quadro só, volta a ser o teleporte que
        // motivou esta classe.
        var avanco = new LungeMotion();
        avanco.Start(0.6f, 0.18f);

        var primeiro = avanco.Consume(Passo);

        Assert.True(primeiro < 0.6f, $"o primeiro quadro percorreu tudo: {primeiro}");
        Assert.True(avanco.IsMoving);
    }

    [Fact]
    public void Duracao_zero_aplica_tudo_de_uma_vez()
    {
        // Golpe cuja janela de acerto abre imediatamente: não há wind-up para
        // distribuir, e prender o avanço seria pior.
        var avanco = new LungeMotion();
        avanco.Start(0.6f, 0f);

        Assert.Equal(0.6f, avanco.Consume(Passo), Tol);
        Assert.False(avanco.IsMoving);
    }

    [Fact]
    public void Distancia_zero_nao_move_nada()
    {
        var avanco = new LungeMotion();
        avanco.Start(0f, 0.18f);

        Assert.False(avanco.IsMoving);
        Assert.Equal(0f, avanco.Consume(Passo));
    }

    [Fact]
    public void Cancelar_interrompe_no_meio()
    {
        var avanco = new LungeMotion();
        avanco.Start(0.6f, 0.18f);
        avanco.Consume(Passo);

        avanco.Cancel();

        Assert.False(avanco.IsMoving);
        Assert.Equal(0f, avanco.Consume(Passo));
    }

    [Fact]
    public void Comecar_de_novo_descarta_o_avanco_anterior()
    {
        // Encadear o próximo golpe substitui o avanço em andamento; somar os
        // dois faria o combo arremessar o personagem para longe.
        var avanco = new LungeMotion();
        avanco.Start(0.6f, 0.18f);
        avanco.Consume(Passo);

        avanco.Start(1.0f, 0.22f);

        Assert.Equal(1.0f, avanco.Remaining, Tol);
    }

    [Fact]
    public void Delta_zero_nao_move_nem_consome()
    {
        var avanco = new LungeMotion();
        avanco.Start(0.6f, 0.18f);

        Assert.Equal(0f, avanco.Consume(0f));
        Assert.Equal(0.6f, avanco.Remaining, Tol);
    }
}
