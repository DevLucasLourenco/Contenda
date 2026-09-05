using Contenda.Weapons;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// A máquina do combo corpo a corpo.
/// </summary>
/// <remarks>
/// Janelas de tempo são onde erro de sinal e de comparação passam despercebidos:
/// o combo "funciona" e só se comporta errado no limite, que é justamente onde o
/// jogador vive. Por isso os limites são testados explicitamente.
/// </remarks>
public sealed class MeleeComboTests
{
    private const float Passo = 1f / 60f;

    /// <summary>Três golpes: janela de acerto 0,18–0,32 s, janela de encadeamento até 0,75 s.</summary>
    private static MeleeCombo Criar(int golpes = 3) => new(
        passos: golpes,
        hitStart: 0.18f,
        hitEnd: 0.32f,
        comboWindowEnd: 0.75f);

    [Fact]
    public void Comeca_ocioso_sem_area_de_dano_ativa()
    {
        var combo = Criar();
        Assert.False(combo.IsAttacking);
        Assert.False(combo.IsHitWindowOpen);
        Assert.Equal(0, combo.Step);
    }

    [Fact]
    public void O_primeiro_pedido_comeca_no_passo_um()
    {
        var combo = Criar();
        Assert.True(combo.TryStart());
        Assert.True(combo.IsAttacking);
        Assert.Equal(1, combo.Step);
    }

    [Fact]
    public void A_area_de_dano_so_existe_dentro_da_janela()
    {
        var combo = Criar();
        combo.TryStart();

        combo.Advance(0.10f);
        Assert.False(combo.IsHitWindowOpen);   // antes

        combo.Advance(0.15f);                  // t = 0,25
        Assert.True(combo.IsHitWindowOpen);    // dentro

        combo.Advance(0.15f);                  // t = 0,40
        Assert.False(combo.IsHitWindowOpen);   // depois
    }

    [Fact]
    public void Encadear_dentro_da_janela_avanca_para_o_proximo_passo()
    {
        var combo = Criar();
        combo.TryStart();
        combo.Advance(0.40f);

        Assert.True(combo.TryStart());
        Assert.Equal(2, combo.Step);

        combo.Advance(0.40f);
        Assert.True(combo.TryStart());
        Assert.Equal(3, combo.Step);
    }

    [Fact]
    public void Depois_do_ultimo_passo_a_sequencia_recomeca()
    {
        var combo = Criar();
        for (var i = 0; i < 3; i++)
        {
            combo.TryStart();
            combo.Advance(0.40f);
        }

        Assert.True(combo.TryStart());
        Assert.Equal(1, combo.Step);
    }

    [Fact]
    public void Perder_a_janela_reinicia_no_passo_um_sem_penalidade()
    {
        var combo = Criar();
        combo.TryStart();
        combo.Advance(0.40f);
        combo.TryStart();
        Assert.Equal(2, combo.Step);

        // Deixa a janela de encadeamento expirar.
        combo.Advance(1.0f);
        Assert.False(combo.IsAttacking);

        // Sem penalidade: o próximo pedido é aceito na hora, no passo 1.
        Assert.True(combo.TryStart());
        Assert.Equal(1, combo.Step);
    }

    [Fact]
    public void Nao_da_para_encadear_antes_da_janela_de_acerto_abrir()
    {
        // Marteladas no botão não podem pular a animação: sem isto, o jogador
        // clica três vezes num quadro e sai o combo inteiro de uma vez.
        var combo = Criar();
        combo.TryStart();
        combo.Advance(0.05f);

        Assert.False(combo.TryStart());
        Assert.Equal(1, combo.Step);
    }

    [Fact]
    public void A_janela_de_acerto_abre_e_fecha_uma_vez_so_por_golpe()
    {
        var combo = Criar();
        combo.TryStart();

        var aberturas = 0;
        var estavaAberta = false;

        for (var t = 0f; t < 0.7f; t += Passo)
        {
            combo.Advance(Passo);
            if (combo.IsHitWindowOpen && !estavaAberta)
                aberturas++;
            estavaAberta = combo.IsHitWindowOpen;
        }

        Assert.Equal(1, aberturas);
    }

    [Fact]
    public void Cancelar_encerra_o_ataque_e_fecha_a_area_de_dano()
    {
        // Morrer ou tomar atordoamento no meio do golpe não pode deixar área de
        // dano ativa órfã — bug previsível da spec 15 §5.
        var combo = Criar();
        combo.TryStart();
        combo.Advance(0.25f);
        Assert.True(combo.IsHitWindowOpen);

        combo.Cancel();

        Assert.False(combo.IsAttacking);
        Assert.False(combo.IsHitWindowOpen);
        Assert.Equal(0, combo.Step);
    }

    [Fact]
    public void Resetar_devolve_ao_estado_de_recem_criado()
    {
        var combo = Criar();
        combo.TryStart();
        combo.Advance(0.40f);
        combo.TryStart();

        combo.Reset();

        Assert.Equal(0, combo.Step);
        Assert.False(combo.IsAttacking);
        Assert.True(combo.TryStart());
        Assert.Equal(1, combo.Step);
    }

    [Fact]
    public void Uma_arma_de_um_golpe_so_nunca_avanca_de_passo()
    {
        var combo = Criar(golpes: 1);
        combo.TryStart();
        combo.Advance(0.40f);

        Assert.True(combo.TryStart());
        Assert.Equal(1, combo.Step);
    }
}
