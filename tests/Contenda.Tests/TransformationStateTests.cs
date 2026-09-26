using Contenda.Components.Transformations;
using Contenda.Weapons;
using Xunit;

namespace Contenda.Tests;

public sealed class TransformationStateTests
{
    [Fact]
    public void Selecao_inclui_Normal_e_da_a_volta_nos_dois_sentidos()
    {
        var state = new TransformationState(1);

        Assert.Equal(1, state.SelectNext());
        Assert.Equal(0, state.SelectNext());
        Assert.Equal(1, state.SelectPrevious());
        Assert.Equal(0, state.SelectPrevious());
    }

    [Fact]
    public void Forma_nao_ativa_sem_mana_ou_quando_Normal_esta_selecionado()
    {
        var state = new TransformationState(1);

        Assert.False(state.TryActivate(100f, 20f));
        state.SelectNext();
        Assert.False(state.TryActivate(19.99f, 20f));
        Assert.False(state.IsActive);
    }

    [Fact]
    public void Duracao_minima_e_reversao_sao_controladas_por_tempo_de_jogo()
    {
        var state = new TransformationState(1);
        state.SelectNext();
        Assert.True(state.TryActivate(20f, 20f));

        state.Advance(0.99f);
        Assert.False(state.CanRevert(1f));
        state.Advance(0.01f);
        Assert.True(state.CanRevert(1f));

        state.Revert();
        Assert.False(state.IsActive);
        Assert.Equal(0f, state.ActiveDuration);
    }

    [Fact]
    public void Reset_limpa_selecao_e_forma_ativa()
    {
        var state = new TransformationState(2);
        state.SelectNext();
        Assert.True(state.TryActivate(20f, 20f));

        state.Reset();

        Assert.Equal(0, state.SelectedIndex);
        Assert.False(state.IsActive);
    }

    [Fact]
    public void Dano_da_explosao_cai_linearmente_ate_sessenta_porcento_na_borda()
    {
        Assert.Equal(1f, ExplosionMath.DamageMultiplier(0f, 2.5f, 0.6f));
        Assert.Equal(0.8f, ExplosionMath.DamageMultiplier(1.25f, 2.5f, 0.6f), 5);
        Assert.Equal(0.6f, ExplosionMath.DamageMultiplier(2.5f, 2.5f, 0.6f), 5);
        Assert.Equal(0.6f, ExplosionMath.DamageMultiplier(4f, 2.5f, 0.6f), 5);
        Assert.Equal(0f, ExplosionMath.DamageMultiplier(0f, 0f, 0.6f));
    }

    [Fact]
    public void Municao_infinita_nao_gasta_cartucho_nem_inicia_recarga()
    {
        var state = new RevolverState(capacidade: 1, reloadTime: 1.6f, infiniteAmmo: true);

        Assert.True(state.TryFire(0.5f));
        state.Advance(0.5f);
        Assert.True(state.TryFire(0.5f));
        Assert.Equal(1, state.Rounds);
        Assert.False(state.IsReloading);
    }
}
