using Contenda.Weapons;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Tambor, cadência e recarga automática do revólver.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>MeleeCombo</c>: cadência e recarga são janelas
/// de tempo, e é exatamente aí que erro de comparação passa despercebido.
/// Ver ticket 09 e spec 07 §5.
/// </remarks>
public sealed class RevolverStateTests
{
    private const float Interval = 0.3f;

    private static RevolverState Criar(int capacidade = 6, float reloadTime = 1.6f)
        => new(capacidade, reloadTime);

    [Fact]
    public void Comeca_com_o_tambor_cheio_e_pronto_para_atirar()
    {
        var tambor = Criar();
        Assert.True(tambor.CanFire);
        Assert.False(tambor.IsReloading);
    }

    [Fact]
    public void Atirar_consome_um_cartucho_e_devolve_verdadeiro()
    {
        var tambor = Criar(capacidade: 6);
        Assert.True(tambor.TryFire(Interval));
    }

    [Fact]
    public void Esvaziar_o_tambor_inicia_recarga_automatica()
    {
        var tambor = Criar(capacidade: 2);
        tambor.TryFire(Interval);
        tambor.Advance(Interval + 0.001f); // limpa a cadência do primeiro tiro
        Assert.True(tambor.CanFire); // ainda sobra 1

        tambor.TryFire(Interval);
        Assert.False(tambor.CanFire); // esvaziou
        Assert.True(tambor.IsReloading);
    }

    [Fact]
    public void Esvaziar_o_tambor_dispara_evento_de_inicio_de_recarga_uma_vez()
    {
        var tambor = Criar(capacidade: 1);
        var recargas = 0;
        tambor.ReloadStarted += () => recargas++;

        Assert.True(tambor.TryFire(Interval));
        Assert.False(tambor.TryFire(Interval));

        Assert.Equal(1, recargas);
    }

    [Fact]
    public void Atirar_com_o_tambor_vazio_nao_faz_nada()
    {
        var tambor = Criar(capacidade: 1);
        tambor.TryFire(Interval);

        Assert.False(tambor.TryFire(Interval));
    }

    [Fact]
    public void Atirar_antes_da_cadencia_passar_e_recusado()
    {
        var tambor = Criar(capacidade: 6);
        tambor.TryFire(Interval);

        Assert.False(tambor.TryFire(Interval));
    }

    [Fact]
    public void Depois_da_cadencia_passar_atirar_de_novo_funciona()
    {
        var tambor = Criar(capacidade: 6);
        tambor.TryFire(Interval);

        tambor.Advance(Interval + 0.001f);

        Assert.True(tambor.TryFire(Interval));
    }

    [Fact]
    public void Recarregar_leva_o_tempo_configurado()
    {
        var tambor = Criar(capacidade: 1, reloadTime: 1.0f);
        tambor.TryFire(Interval);
        Assert.True(tambor.IsReloading);

        tambor.Advance(0.5f);
        Assert.True(tambor.IsReloading);

        tambor.Advance(0.5f);
        Assert.False(tambor.IsReloading);
    }

    [Fact]
    public void Recarga_completa_enche_o_tambor_de_novo()
    {
        var tambor = Criar(capacidade: 1, reloadTime: 1.0f);
        tambor.TryFire(Interval);
        tambor.Advance(1.0f);

        Assert.True(tambor.CanFire);
        Assert.True(tambor.TryFire(Interval));
    }

    [Fact]
    public void Atirar_durante_a_recarga_e_recusado()
    {
        var tambor = Criar(capacidade: 1, reloadTime: 1.0f);
        tambor.TryFire(Interval);

        tambor.Advance(0.9f); // quase pronta, ainda recarregando
        Assert.False(tambor.TryFire(Interval));
    }

    [Fact]
    public void A_cadencia_usa_o_intervalo_passado_a_cada_chamada_nao_um_fixo()
    {
        // AttackSpeed do StatBlock divide o AttackInterval a cada tiro -- se o
        // intervalo fosse fixado na construção, o atributo não pegaria efeito
        // sem tocar nos dados da arma. Ver ticket 09.
        var tambor = Criar(capacidade: 6);
        tambor.TryFire(Interval); // cadência 0,3s

        tambor.Advance(Interval + 0.001f); // limpa a cadência do primeiro tiro
        Assert.True(tambor.TryFire(0.1f)); // pede o segundo com cadência menor

        tambor.Advance(0.05f); // não bastaria para os 0,3s originais...
        Assert.False(tambor.TryFire(0.1f));

        tambor.Advance(0.06f); // ...mas a cadência ATUAL era só 0,1s
        Assert.True(tambor.TryFire(0.1f));
    }

    [Fact]
    public void ResetForSpawn_enche_o_tambor_e_cancela_recarga_e_cadencia()
    {
        var tambor = Criar(capacidade: 2, reloadTime: 1.0f);
        tambor.TryFire(Interval);
        tambor.TryFire(Interval); // esvazia, começa a recarregar

        tambor.ResetForSpawn();

        Assert.False(tambor.IsReloading);
        Assert.True(tambor.CanFire);
        Assert.True(tambor.TryFire(Interval));
    }

    [Fact]
    public void Capacidade_menor_que_um_vira_um()
    {
        var tambor = Criar(capacidade: 0);
        Assert.True(tambor.TryFire(Interval));
        Assert.False(tambor.CanFire);
        Assert.True(tambor.IsReloading);
    }
}
