using Contenda.Components.Mana;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Mana: consumo atômico, consumo parcial e o atraso da regeneração.
/// </summary>
/// <remarks>
/// A distinção entre consumo atômico (habilidades, M3) e parcial (dreno de
/// transformação, M4) não é detalhe de implementação — ver ticket 10. Testar
/// os dois aqui é o que evita descobrir a escolha errada só no M4, como uma
/// transformação que não reverte.
/// </remarks>
public sealed class ManaStateTests
{
    private const float Tol = 0.001f;
    private const float Delay = 1.0f;

    [Fact]
    public void Nasce_com_a_mana_cheia_por_padrao()
    {
        var mana = new ManaState(100f, Delay);
        Assert.Equal(100f, mana.Current, Tol);
        Assert.Equal(1f, mana.Percent, Tol);
    }

    [Fact]
    public void Pode_nascer_com_percentual_inicial_parcial()
    {
        var mana = new ManaState(100f, Delay, startingPercent: 0.5f);
        Assert.Equal(50f, mana.Current, Tol);
    }

    [Fact]
    public void Consumo_atomico_desconta_o_valor()
    {
        var mana = new ManaState(100f, Delay);
        Assert.True(mana.TryConsume(30f));
        Assert.Equal(70f, mana.Current, Tol);
    }

    [Fact]
    public void Consumo_atomico_sem_saldo_nao_altera_nada()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(40f); // sobra 60

        Assert.False(mana.TryConsume(61f));
        Assert.Equal(60f, mana.Current, Tol);
    }

    [Fact]
    public void Consumo_atomico_no_limite_exato_funciona()
    {
        var mana = new ManaState(100f, Delay);
        Assert.True(mana.TryConsume(100f));
        Assert.Equal(0f, mana.Current, Tol);
    }

    [Fact]
    public void CanConsume_reflete_se_o_proximo_TryConsume_passaria()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(70f); // sobra 30

        Assert.True(mana.CanConsume(30f));
        Assert.False(mana.CanConsume(30.01f));
    }

    [Fact]
    public void Drain_parcial_leva_o_que_houver_e_nunca_falha()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(90f); // sobra 10

        var consumido = mana.Drain(25f); // pede mais do que existe

        Assert.Equal(10f, consumido, Tol);
        Assert.Equal(0f, mana.Current, Tol);
    }

    [Fact]
    public void Drain_com_saldo_suficiente_consome_exatamente_o_pedido()
    {
        var mana = new ManaState(100f, Delay);
        var consumido = mana.Drain(15f);

        Assert.Equal(15f, consumido, Tol);
        Assert.Equal(85f, mana.Current, Tol);
    }

    [Fact]
    public void Drain_avisa_ao_zerar()
    {
        var mana = new ManaState(50f, Delay);
        var zerou = 0;
        mana.Depleted += () => zerou++;

        mana.Drain(50f);

        Assert.Equal(1, zerou);
    }

    [Fact]
    public void Consumo_atomico_tambem_avisa_ao_zerar()
    {
        var mana = new ManaState(50f, Delay);
        var zerou = 0;
        mana.Depleted += () => zerou++;

        mana.TryConsume(50f);

        Assert.Equal(1, zerou);
    }

    [Fact]
    public void Drain_sem_mana_nenhuma_nao_avisa_de_novo()
    {
        var mana = new ManaState(50f, Delay);
        var zerou = 0;
        mana.Depleted += () => zerou++;

        mana.Drain(50f);
        mana.Drain(10f); // já estava em zero

        Assert.Equal(1, zerou);
    }

    [Fact]
    public void Gastar_pausa_a_regeneracao_por_cerca_de_um_segundo()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(50f);

        mana.Advance(0.5f, regenPerSecond: 100f); // ainda dentro da pausa

        Assert.Equal(50f, mana.Current, Tol);
    }

    [Fact]
    public void Passada_a_pausa_a_mana_volta_a_subir()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(50f);

        // Um Advance só, cobrindo a pausa inteira (1,0 s) e mais 0,1 s depois
        // dela: a regeneração usa o delta INTEIRO deste tique assim que a
        // pausa zera, sem fracionar o quadro -- mesma escolha do HealthState
        // para a invulnerabilidade.
        mana.Advance(Delay + 0.1f, regenPerSecond: 10f);

        Assert.Equal(61f, mana.Current, Tol);
    }

    [Fact]
    public void Regeneracao_nao_acontece_no_mesmo_tique_que_ainda_esta_pausado()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(50f);

        mana.Advance(Delay - 0.001f, regenPerSecond: 10f); // um triz antes de liberar

        Assert.Equal(50f, mana.Current, Tol);
    }

    [Fact]
    public void Regeneracao_nunca_passa_do_maximo()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(1f);

        mana.Advance(Delay + 10f, regenPerSecond: 1000f);

        Assert.Equal(100f, mana.Current, Tol);
    }

    [Fact]
    public void Cada_novo_gasto_reinicia_a_pausa_da_regeneracao()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(50f);

        mana.Advance(0.9f, regenPerSecond: 100f); // quase no fim da pausa original
        mana.TryConsume(10f); // gasta de novo: pausa reinicia

        mana.Advance(0.5f, regenPerSecond: 100f); // só 0,5 s desde o segundo gasto

        Assert.Equal(40f, mana.Current, Tol); // ainda pausada, nada regenerou
    }

    [Fact]
    public void A_regeneracao_usa_a_taxa_passada_a_cada_chamada_nao_uma_fixa()
    {
        // ManaRegen do StatBlock pode mudar (transformação, M4) sem editar
        // ManaDefinition -- se a taxa fosse fixada na construção, o atributo
        // não teria efeito depois do primeiro Advance.
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(50f);
        mana.Advance(Delay + 0.001f, regenPerSecond: 0f); // limpa a pausa, sem regenerar

        mana.Advance(1f, regenPerSecond: 20f);
        Assert.Equal(70f, mana.Current, Tol);

        mana.Advance(1f, regenPerSecond: 5f);
        Assert.Equal(75f, mana.Current, Tol);
    }

    [Fact]
    public void Restore_nao_passa_do_maximo()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(90f);

        mana.Restore(999f);

        Assert.Equal(100f, mana.Current, Tol);
    }

    [Fact]
    public void Mudar_o_maximo_preserva_a_fracao()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(50f);
        Assert.Equal(0.5f, mana.Percent, Tol);

        mana.SetMax(200f);
        Assert.Equal(100f, mana.Current, Tol);
        Assert.Equal(0.5f, mana.Percent, Tol);
    }

    [Fact]
    public void Resetar_respeita_o_percentual_inicial_em_vez_de_encher()
    {
        // Um boss com StartingManaPercent parcial (balanceamento) reciclado
        // pelo pool tem que voltar à MESMA fração de nascença, não a 100%.
        var mana = new ManaState(100f, Delay, startingPercent: 0.4f);
        mana.Restore(999f); // enche manualmente, simulando uso em partida

        mana.Reset();

        Assert.Equal(40f, mana.Current, Tol);
    }

    [Fact]
    public void Resetar_devolve_ao_estado_de_recem_criado()
    {
        var mana = new ManaState(100f, Delay);
        mana.TryConsume(80f);

        mana.Reset();

        Assert.Equal(100f, mana.Current, Tol);

        // A pausa também é limpa: regenerar não deveria ficar bloqueado por
        // uma pausa herdada de antes do reset.
        mana.TryConsume(10f);
        mana.Advance(Delay + 0.1f, regenPerSecond: 10f);
        Assert.True(mana.Current > 90f);
    }
}
