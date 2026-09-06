using Contenda.Components.Movement;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Coyote time e jump buffer — ticket 17, spec 16 §3.
/// </summary>
public sealed class JumpStateTests
{
    private const float Coyote = 0.12f;
    private const float Buffer = 0.12f;
    private const float Passo = 1f / 60f;

    [Fact]
    public void No_chao_sempre_pode_pular()
    {
        var pulo = new JumpState();
        pulo.Advance(Passo, noChao: true, Coyote);

        Assert.True(pulo.CanJump);
    }

    [Fact]
    public void Recem_criado_no_ar_nao_pode_pular()
    {
        var pulo = new JumpState();
        pulo.Advance(Passo, noChao: false, Coyote);

        Assert.False(pulo.CanJump);
    }

    [Fact]
    public void Coyote_time_permite_pular_logo_depois_de_sair_do_chao()
    {
        var pulo = new JumpState();
        pulo.Advance(Passo, noChao: true, Coyote);

        // Saiu da borda: alguns quadros no ar, ainda dentro da janela de coyote.
        pulo.Advance(Passo, noChao: false, Coyote);
        pulo.Advance(Passo, noChao: false, Coyote);

        Assert.True(pulo.CanJump);
    }

    [Fact]
    public void Coyote_time_expira_depois_da_janela()
    {
        var pulo = new JumpState();
        pulo.Advance(Passo, noChao: true, Coyote);

        var quadros = Mathf_CeilDiv(Coyote, Passo) + 5;
        for (var i = 0; i < quadros; i++)
            pulo.Advance(Passo, noChao: false, Coyote);

        Assert.False(pulo.CanJump);
    }

    [Fact]
    public void Jump_buffer_registra_o_pedido_e_expira_sozinho()
    {
        var pulo = new JumpState();
        pulo.RequestJump(Buffer);

        Assert.True(pulo.HasBufferedJump);

        var quadros = Mathf_CeilDiv(Buffer, Passo) + 5;
        for (var i = 0; i < quadros; i++)
            pulo.Advance(Passo, noChao: false, Coyote);

        Assert.False(pulo.HasBufferedJump);
    }

    [Fact]
    public void Pedido_de_pulo_pouco_antes_de_aterrissar_sobrevive_ate_o_chao()
    {
        var pulo = new JumpState();

        // No ar, pede o pulo um pouco antes de tocar o chão.
        pulo.Advance(Passo, noChao: false, Coyote);
        pulo.RequestJump(Buffer);
        pulo.Advance(Passo, noChao: false, Coyote);
        pulo.Advance(Passo, noChao: false, Coyote);

        // Agora aterrissa: o pedido ainda está dentro da janela do buffer.
        pulo.Advance(Passo, noChao: true, Coyote);

        Assert.True(pulo.HasBufferedJump);
        Assert.True(pulo.CanJump);
    }

    [Fact]
    public void Consume_zera_as_duas_janelas()
    {
        var pulo = new JumpState();
        pulo.Advance(Passo, noChao: true, Coyote);
        pulo.RequestJump(Buffer);

        pulo.Consume();

        Assert.False(pulo.CanJump);
        Assert.False(pulo.HasBufferedJump);
    }

    [Fact]
    public void Consume_impede_pular_de_novo_no_mesmo_instante_da_aterrissagem()
    {
        var pulo = new JumpState();
        pulo.Advance(Passo, noChao: true, Coyote);
        pulo.RequestJump(Buffer);
        pulo.Consume();

        // Ainda no mesmo quadro "no chão": sem um novo pedido, não pula de novo.
        pulo.Advance(Passo, noChao: true, Coyote);
        Assert.False(pulo.HasBufferedJump);
    }

    [Fact]
    public void Coyote_time_nao_pula_mais_exatamente_no_quadro_em_que_a_janela_esgota()
    {
        var pulo = new JumpState();
        pulo.Advance(Passo, noChao: true, Coyote);

        // Avança pelo EXATO número de quadros que zera a janela -- nem um a
        // mais, nem um a menos -- para testar a borda `> 0f` diretamente,
        // não com folga de sobra como Coyote_time_expira_depois_da_janela.
        var quadrosParaZerar = Mathf_CeilDiv(Coyote, Passo);
        for (var i = 0; i < quadrosParaZerar; i++)
            pulo.Advance(Passo, noChao: false, Coyote);

        Assert.False(pulo.CanJump);
    }

    [Fact]
    public void Jump_buffer_nao_vale_mais_exatamente_no_quadro_em_que_a_janela_esgota()
    {
        var pulo = new JumpState();
        pulo.RequestJump(Buffer);

        var quadrosParaZerar = Mathf_CeilDiv(Buffer, Passo);
        for (var i = 0; i < quadrosParaZerar; i++)
            pulo.Advance(Passo, noChao: false, Coyote);

        Assert.False(pulo.HasBufferedJump);
    }

    [Fact]
    public void Reset_devolve_ao_estado_de_recem_criado()
    {
        var pulo = new JumpState();
        pulo.Advance(Passo, noChao: true, Coyote);
        pulo.RequestJump(Buffer);

        pulo.Reset();

        Assert.False(pulo.CanJump);
        Assert.False(pulo.HasBufferedJump);
    }

    private static int Mathf_CeilDiv(float total, float passo) => (int)System.Math.Ceiling(total / passo);
}
