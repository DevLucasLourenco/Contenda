using System.Collections.Generic;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>
/// Recarga por habilidade, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>ActionLockSet</c>. Guarda quando cada
/// habilidade fica pronta de novo (<c>ReadyAt</c>), não um contador regressivo
/// — consultar não precisa de um <c>Tick</c> por quadro, só do relógio de
/// quem chama. Ver spec 05 §6 e o ticket 14.
///
/// A fonte é <c>string</c>, não <c>StringName</c>: construir <c>StringName</c>
/// fora do processo do Godot derruba o processo — mesma escolha já feita no
/// <c>ActionLockSet</c> e no <c>DamageInfo.SourceTag</c>.
/// </remarks>
public sealed class AbilityCooldownTracker
{
    private readonly Dictionary<string, float> _prontaAs = new();

    /// <summary>Se a habilidade já pode ser executada de novo.</summary>
    public bool IsReady(string abilityId, float now) => RemainingAt(abilityId, now) <= 0f;

    /// <summary>Quanto falta para a habilidade ficar pronta, em segundos. Nunca negativo.</summary>
    public float RemainingAt(string abilityId, float now)
    {
        if (!_prontaAs.TryGetValue(abilityId, out var prontaAs))
            return 0f;

        return Mathf.Max(0f, prontaAs - now);
    }

    /// <summary>
    /// Inicia a recarga. Chamado no INÍCIO da execução, não no fim — spec 05 §4.
    /// </summary>
    /// <remarks>
    /// Substitui a recarga em andamento, não soma: reexecutar não deveria
    /// empilhar tempo de espera.
    /// </remarks>
    public void Start(string abilityId, float duration, float now)
    {
        if (duration <= 0f)
        {
            _prontaAs.Remove(abilityId);
            return;
        }

        _prontaAs[abilityId] = now + duration;
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void Reset() => _prontaAs.Clear();
}
