namespace Contenda.Components.Abilities;

/// <summary>
/// A ordem de validação de uma tentativa de execução, sem nenhum componente envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito: a ordem de gates é exatamente o tipo de regra que muda
/// de lugar sem ninguém perceber num método grande dentro de um <c>Node</c>,
/// e é o coração do critério de aceite do ticket 14. Extraída para xUnit
/// cobrir a ordem da spec 05 §2 diretamente, em vez de só pela árvore real do
/// <c>AbilityProbe</c>.
///
/// Mana fica de fora de propósito: é o único gate que também MUTA estado
/// (<c>ManaComponent.TryConsume</c> já é atômico), então <c>AbilityComponent</c>
/// tenta consumir diretamente depois que este método aprova o resto — duas
/// checagens (`CanConsume` e depois `TryConsume`) nesta função pura não
/// protegeriam contra nada que `TryConsume` sozinho não já garanta.
/// </remarks>
public static class AbilityGateEvaluator
{
    /// <param name="alreadyCasting">Já existe uma execução em andamento.</param>
    /// <param name="blockedByLock">O agregado de <c>ActionLock</c> do personagem contém <c>Abilities</c>.</param>
    /// <param name="onCooldown">A habilidade ainda não recarregou.</param>
    public static AbilityAttemptResult Evaluate(bool alreadyCasting, bool blockedByLock, bool onCooldown)
    {
        if (alreadyCasting)
            return AbilityAttemptResult.AlreadyCasting;

        if (blockedByLock)
            return AbilityAttemptResult.Blocked;

        if (onCooldown)
            return AbilityAttemptResult.OnCooldown;

        // Tags requeridas/bloqueadoras (spec 05 §2, passos 4-5) ficam de fora:
        // não existe nenhuma fonte real de tag até o M4, então não há nada
        // para avaliar ainda -- ver o comentário equivalente em AbilityComponent.
        return AbilityAttemptResult.Success;
    }
}
