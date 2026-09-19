using Contenda.GameModes.Horde;
using Xunit;

namespace Contenda.Tests;

/// <summary>Quando uma onda está limpa, e o alçapão de inimigo preso — ticket 27, spec 10 §4.</summary>
public sealed class WaveClearTimerTests
{
    [Fact]
    public void Enquanto_ha_inimigos_restantes_nunca_limpa()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        Assert.False(timer.TickWaitingForClear(delta: 1f, enemiesRemaining: 3, realActiveCount: 3));
    }

    [Fact]
    public void Pool_zerado_no_mesmo_instante_limpa_na_hora_sem_alcapao()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        var limpou = timer.TickWaitingForClear(delta: 0.016f, enemiesRemaining: 0, realActiveCount: 0);

        Assert.True(limpou);
        Assert.False(timer.StuckFallbackTriggered);
    }

    [Fact]
    public void Pool_ainda_ativo_logo_apos_o_ultimo_abate_nao_limpa_ainda()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        // DeathDuration (o corpo ainda visível) é bem menor que o alçapão --
        // 1 segundo de discordância não deveria bastar para forçar nada.
        Assert.False(timer.TickWaitingForClear(delta: 1f, enemiesRemaining: 0, realActiveCount: 1));
    }

    [Fact]
    public void Discordancia_alem_do_alcapao_limpa_e_marca_o_alcapao_disparado()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        for (var i = 0; i < 4; i++)
            Assert.False(timer.TickWaitingForClear(delta: 1f, enemiesRemaining: 0, realActiveCount: 1));

        var limpou = timer.TickWaitingForClear(delta: 1.5f, enemiesRemaining: 0, realActiveCount: 1);

        Assert.True(limpou);
        Assert.True(timer.StuckFallbackTriggered);
    }

    [Fact]
    public void Pool_zerar_antes_do_alcapao_vencer_nunca_aciona_o_alcapao()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        Assert.False(timer.TickWaitingForClear(delta: 2f, enemiesRemaining: 0, realActiveCount: 1));

        var limpou = timer.TickWaitingForClear(delta: 0.1f, enemiesRemaining: 0, realActiveCount: 0);

        Assert.True(limpou);
        Assert.False(timer.StuckFallbackTriggered);
    }

    [Fact]
    public void Inimigo_novo_contado_depois_reseta_o_relogio_de_travado()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        Assert.False(timer.TickWaitingForClear(delta: 4f, enemiesRemaining: 0, realActiveCount: 1));
        // Voltou a ter "restante" (ex.: reforço contínuo do chefe, spec 10 §5) -- o alçapão não deveria já estar quase disparando.
        Assert.False(timer.TickWaitingForClear(delta: 0.1f, enemiesRemaining: 1, realActiveCount: 2));
        Assert.False(timer.TickWaitingForClear(delta: 4.9f, enemiesRemaining: 0, realActiveCount: 1));
    }

    [Fact]
    public void Respiro_nao_termina_antes_do_tempo()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        Assert.False(timer.TickResting(3.9f));
    }

    [Fact]
    public void Respiro_termina_exatamente_no_tempo()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        Assert.True(timer.TickResting(4f));
    }

    [Fact]
    public void Respiro_acumula_entre_chamadas()
    {
        var timer = new WaveClearTimer(completionDelay: 4f, stuckFallbackSeconds: 5f);

        Assert.False(timer.TickResting(2f));
        Assert.True(timer.TickResting(2.1f));
    }
}
