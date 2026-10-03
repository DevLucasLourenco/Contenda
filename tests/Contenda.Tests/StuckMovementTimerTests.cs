using Contenda.Components.AI;
using Xunit;

namespace Contenda.Tests;

public sealed class StuckMovementTimerTests
{
    [Fact]
    public void Persegue_sem_sair_do_lugar_acumula_tempo_parado()
    {
        var timer = new StuckMovementTimer(minimumProgressSpeed: 0.1f);

        timer.Advance(delta: 2f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0f);
        timer.Advance(delta: 1.5f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0.05f);

        Assert.Equal(3.5f, timer.ElapsedSeconds, 3);
    }

    [Fact]
    public void Movimento_real_reinicia_o_tempo_parado()
    {
        var timer = new StuckMovementTimer(minimumProgressSpeed: 0.1f);
        timer.Advance(delta: 2f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0f);

        timer.Advance(delta: 0.2f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0.11f);

        Assert.Equal(0f, timer.ElapsedSeconds);
    }

    [Fact]
    public void So_marca_remocao_uma_vez_depois_do_limite()
    {
        var timer = new StuckMovementTimer(minimumProgressSpeed: 0.1f);
        timer.Advance(4.9f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0f);

        Assert.False(timer.TryMarkForRemoval(timeoutSeconds: 5f));

        timer.Advance(0.1f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0f);

        Assert.True(timer.TryMarkForRemoval(timeoutSeconds: 5f));
        Assert.False(timer.TryMarkForRemoval(timeoutSeconds: 5f));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Sem_perseguicao_ou_sem_intencao_de_movimento_nao_conta_como_travado(
        bool isChasing,
        bool hasMoveIntent)
    {
        var timer = new StuckMovementTimer(minimumProgressSpeed: 0.1f);
        timer.Advance(delta: 2f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0f);

        timer.Advance(0.2f, isChasing, hasMoveIntent, horizontalSpeed: 0f);

        Assert.Equal(0f, timer.ElapsedSeconds);
    }

    [Fact]
    public void Reset_rearma_a_remocao_de_um_inimigo_reciclado()
    {
        var timer = new StuckMovementTimer(minimumProgressSpeed: 0.1f);
        timer.Advance(5f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0f);
        Assert.True(timer.TryMarkForRemoval(timeoutSeconds: 5f));

        timer.Reset();
        timer.Advance(5f, isChasing: true, hasMoveIntent: true, horizontalSpeed: 0f);

        Assert.True(timer.TryMarkForRemoval(timeoutSeconds: 5f));
    }
}
