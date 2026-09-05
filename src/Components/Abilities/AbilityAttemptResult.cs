namespace Contenda.Components.Abilities;

/// <summary>Por que uma tentativa de executar uma habilidade passou ou falhou.</summary>
/// <remarks>
/// Não inclui "sequência não casou com nada" — isso acontece ANTES de existir
/// uma <see cref="AbilityDefinition"/> para tentar, e por isso é um evento à
/// parte (<c>AbilityComponent.SequenceRejected</c>), não um valor aqui. Ver
/// spec 05 §2 e §7.
/// </remarks>
public enum AbilityAttemptResult : byte
{
    Success,
    OnCooldown,
    NotEnoughMana,
    Blocked,
    AlreadyCasting,
    TagRequirement,
}
