using Contenda.Components.Abilities;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// O relógio de uma execução de habilidade — ticket 14, spec 05 §4.
/// </summary>
public sealed class AbilityTimelineTests
{
    private const float Tol = 0.001f;

    [Fact]
    public void Comeca_ociosa()
    {
        var relogio = new AbilityTimeline();
        Assert.False(relogio.IsActive);
    }

    [Fact]
    public void Begin_ativa_e_zera_o_relogio()
    {
        var relogio = new AbilityTimeline();
        relogio.Begin(castTime: 0.10f, recoveryTime: 0.25f);

        Assert.True(relogio.IsActive);
        Assert.Equal(0f, relogio.Elapsed, Tol);
    }

    [Fact]
    public void Advance_acumula_e_continua_ativa_antes_do_fim()
    {
        var relogio = new AbilityTimeline();
        relogio.Begin(castTime: 0.10f, recoveryTime: 0.25f);

        var terminou = relogio.Advance(0.20f);

        Assert.False(terminou);
        Assert.True(relogio.IsActive);
        Assert.Equal(0.20f, relogio.Elapsed, Tol);
    }

    [Fact]
    public void Advance_devolve_verdadeiro_so_no_quadro_que_estoura_a_duracao()
    {
        var relogio = new AbilityTimeline();
        relogio.Begin(castTime: 0.10f, recoveryTime: 0.25f); // total 0.35

        Assert.False(relogio.Advance(0.20f));
        Assert.False(relogio.Advance(0.10f)); // 0.30, ainda dentro
        Assert.True(relogio.Advance(0.10f));  // 0.40, estourou agora
        Assert.False(relogio.IsActive);
    }

    [Fact]
    public void Advance_sem_execucao_ativa_nunca_devolve_verdadeiro()
    {
        var relogio = new AbilityTimeline();
        Assert.False(relogio.Advance(10f));
    }

    [Fact]
    public void Cancel_encerra_antes_do_relogio_estourar()
    {
        var relogio = new AbilityTimeline();
        relogio.Begin(castTime: 0.10f, recoveryTime: 0.25f);
        relogio.Advance(0.05f);

        relogio.Cancel();

        Assert.False(relogio.IsActive);
    }

    [Fact]
    public void Um_novo_Begin_reinicia_mesmo_com_execucao_anterior_em_andamento()
    {
        var relogio = new AbilityTimeline();
        relogio.Begin(castTime: 0.10f, recoveryTime: 0.25f);
        relogio.Advance(0.30f);

        relogio.Begin(castTime: 0.20f, recoveryTime: 0.50f);

        Assert.True(relogio.IsActive);
        Assert.Equal(0f, relogio.Elapsed, Tol);
    }
}
