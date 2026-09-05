using Contenda.Components.Abilities;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Ordem de validação de uma tentativa de execução — ticket 14, spec 05 §2.
/// </summary>
public sealed class AbilityGateEvaluatorTests
{
    [Fact]
    public void Sem_nenhum_bloqueio_aprova()
    {
        var resultado = AbilityGateEvaluator.Evaluate(alreadyCasting: false, blockedByLock: false, onCooldown: false);
        Assert.Equal(AbilityAttemptResult.Success, resultado);
    }

    [Fact]
    public void Ja_executando_reprova_primeiro_mesmo_com_outros_bloqueios_tambem_ativos()
    {
        var resultado = AbilityGateEvaluator.Evaluate(alreadyCasting: true, blockedByLock: true, onCooldown: true);
        Assert.Equal(AbilityAttemptResult.AlreadyCasting, resultado);
    }

    [Fact]
    public void Travado_por_ActionLock_reprova_antes_da_recarga()
    {
        var resultado = AbilityGateEvaluator.Evaluate(alreadyCasting: false, blockedByLock: true, onCooldown: true);
        Assert.Equal(AbilityAttemptResult.Blocked, resultado);
    }

    [Fact]
    public void Em_recarga_reprova_quando_mais_nada_bloqueia()
    {
        var resultado = AbilityGateEvaluator.Evaluate(alreadyCasting: false, blockedByLock: false, onCooldown: true);
        Assert.Equal(AbilityAttemptResult.OnCooldown, resultado);
    }
}
