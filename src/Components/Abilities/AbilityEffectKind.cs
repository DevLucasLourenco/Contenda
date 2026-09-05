namespace Contenda.Components.Abilities;

/// <summary>
/// Como o efeito de uma habilidade acontece. Seleciona um <see cref="IAbilityBehavior"/>
/// no <see cref="AbilityBehaviorRegistry"/>.
/// </summary>
/// <remarks>
/// Vocabulário fixo da spec 05 §3. Só <see cref="DashAttack"/> tem
/// comportamento implementado no ticket 14 — os outros chegam no ticket 15,
/// reaproveitando esta mesma enumeração e o mesmo `AbilityComponent`. Um
/// `Kind` sem entrada no registro falha alto em debug, como o
/// `WeaponFactory` já faz para `WeaponKind.Hitscan` antes do ticket 09.
/// </remarks>
public enum AbilityEffectKind : byte
{
    /// <summary>Cone à frente — Spin Slash, Cyclone.</summary>
    MeleeArc,

    /// <summary>Desloca e aplica dano no trajeto — Dash Slash, Heavy Lunge.</summary>
    DashAttack,

    /// <summary>Dano e lança o alvo para cima — Rising Slash.</summary>
    Uppercut,

    /// <summary>Raycast instantâneo — Quick Step Shot, Deadeye.</summary>
    HitscanShot,

    /// <summary>N hitscans em sequência — Fan The Hammer.</summary>
    HitscanBurst,

    /// <summary>Instancia um projétil — Explosive Shot.</summary>
    Projectile,

    /// <summary>Só aplica modificadores temporários. Pós-MVP.</summary>
    SelfBuff,
}
