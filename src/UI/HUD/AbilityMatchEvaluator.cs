using System;
using System.Collections.Generic;
using Contenda.Input;

namespace Contenda.UI.HUD;

/// <summary>Como a sequência de uma habilidade se relaciona com o que foi digitado até agora.</summary>
public enum AbilityMatchState
{
    /// <summary>Nada foi digitado ainda.</summary>
    Neutral,

    /// <summary>O digitado é um prefixo válido da sequência, mas ainda não é ela inteira.</summary>
    PartialMatch,

    /// <summary>O digitado diverge da sequência, ou já a ultrapassou. Não pode mais completar.</summary>
    Impossible,

    /// <summary>O digitado é exatamente a sequência inteira — pronta para confirmar.</summary>
    Complete,
}

/// <summary>
/// Decide o <see cref="AbilityMatchState"/> de uma linha do guia de combos, sem nó nenhum envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>AbilityGateEvaluator</c> do ticket 14: é a
/// regra visual central do ticket 16 ("acende o que já foi digitado, apaga o
/// que não casa mais"), e é exatamente o tipo de comparação por índice onde um
/// off-by-one passa despercebido — vale testar isolado, sem precisar de um
/// <c>AbilityGuide</c> inteiro na árvore.
/// </remarks>
public static class AbilityMatchEvaluator
{
    public static AbilityMatchState Evaluate(IReadOnlyList<CommandDirection> sequencia, ReadOnlySpan<CommandDirection> digitado)
    {
        if (digitado.Length == 0)
            return AbilityMatchState.Neutral;

        var comprimentoComum = Math.Min(sequencia.Count, digitado.Length);
        for (var i = 0; i < comprimentoComum; i++)
        {
            if (digitado[i] != sequencia[i])
                return AbilityMatchState.Impossible;
        }

        // Digitou mais do que a sequência tem: mesmo com o prefixo batendo,
        // confirmar agora executaria outra habilidade (ou nenhuma), nunca esta.
        if (digitado.Length > sequencia.Count)
            return AbilityMatchState.Impossible;

        return digitado.Length == sequencia.Count ? AbilityMatchState.Complete : AbilityMatchState.PartialMatch;
    }
}
