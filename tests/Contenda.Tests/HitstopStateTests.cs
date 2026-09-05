using Contenda.Components.Health;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Congelamento local de um personagem por um instante — ticket 11.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>ActionLockSet</c>: é uma janela de tempo, e é
/// onde erro de comparação passa despercebido. <c>Engine.TimeScale</c> nunca
/// entra aqui — congelaria a horda inteira junto no M5; ver o ticket.
/// </remarks>
public sealed class HitstopStateTests
{
    private const float Tol = 0.001f;

    [Fact]
    public void Comeca_sem_congelamento()
    {
        var hitstop = new HitstopState();
        Assert.False(hitstop.IsActive);
        Assert.Equal(1f, hitstop.TimeScale, Tol);
    }

    [Fact]
    public void Aplicar_congela_pela_duracao_pedida()
    {
        var hitstop = new HitstopState();
        hitstop.Apply(0.09f);

        Assert.True(hitstop.IsActive);
        Assert.Equal(0f, hitstop.TimeScale, Tol);
    }

    [Fact]
    public void Advance_encerra_o_congelamento_no_tempo_certo()
    {
        var hitstop = new HitstopState();
        hitstop.Apply(0.04f);

        hitstop.Advance(0.03f);
        Assert.True(hitstop.IsActive);

        hitstop.Advance(0.01f);
        Assert.False(hitstop.IsActive);
        Assert.Equal(1f, hitstop.TimeScale, Tol);
    }

    [Fact]
    public void Uma_duracao_menor_nao_encurta_o_congelamento_em_andamento()
    {
        // Um finalizador (0,09s) seguido de um golpe normal (0,04s) enquanto
        // ainda congelado não pode encurtar o congelamento mais longo já em
        // andamento -- ver ActionLockSet, mesmo motivo.
        var hitstop = new HitstopState();
        hitstop.Apply(0.09f);
        hitstop.Advance(0.05f); // restam 0,04s

        hitstop.Apply(0.02f); // pedido menor que o que já resta

        hitstop.Advance(0.03f);
        Assert.True(hitstop.IsActive, "o congelamento mais longo não deveria ter encurtado");
    }

    [Fact]
    public void Uma_duracao_maior_estende_o_congelamento_em_andamento()
    {
        var hitstop = new HitstopState();
        hitstop.Apply(0.04f);
        hitstop.Advance(0.02f); // restam 0,02s

        hitstop.Apply(0.09f); // finalizador chega no meio do congelamento normal

        hitstop.Advance(0.05f);
        Assert.True(hitstop.IsActive);
    }

    [Fact]
    public void Duracao_zero_ou_negativa_nao_congela()
    {
        var hitstop = new HitstopState();
        hitstop.Apply(0f);
        Assert.False(hitstop.IsActive);

        hitstop.Apply(-1f);
        Assert.False(hitstop.IsActive);
    }

    [Fact]
    public void Reset_encerra_o_congelamento_na_hora()
    {
        var hitstop = new HitstopState();
        hitstop.Apply(10f);

        hitstop.Reset();

        Assert.False(hitstop.IsActive);
        Assert.Equal(1f, hitstop.TimeScale, Tol);
    }
}
