namespace Contenda.Components.Abilities;

/// <summary>
/// Como o efeito de uma habilidade acontece. Seleciona um <see cref="IAbilityBehavior"/>
/// no <see cref="AbilityBehaviorRegistry"/>.
/// </summary>
/// <remarks>
/// Vocabulário fixo da spec 05 §3. Todo o catálogo do MVP (spec 05 §5) tem
/// comportamento implementado a partir do ticket 15, exceto
/// <see cref="SelfBuff"/> — pós-MVP, sem consumidor no catálogo. Um `Kind`
/// sem entrada no <c>AbilityBehaviorRegistry</c> falha alto em debug, como o
/// `WeaponFactory` já faz para `WeaponKind.Hitscan` antes do ticket 09.
/// </remarks>
public enum AbilityEffectKind : byte
{
    /// <summary>Cone à frente — Spin Slash.</summary>
    MeleeArc,

    /// <summary>
    /// Desloca e aplica dano no trajeto — Dash Slash, Heavy Lunge e Quick Step
    /// Shot (que só usa um <c>Range</c> maior que o <c>DashDistance</c>, sem
    /// precisar de um `Kind` próprio — ver <see cref="DashAttackBehavior"/>).
    /// </summary>
    DashAttack,

    /// <summary>Dano e lança o alvo para cima — Rising Slash.</summary>
    Uppercut,

    /// <summary>Raycast instantâneo e perfurante — Deadeye.</summary>
    HitscanShot,

    /// <summary>N hitscans em sequência — Fan The Hammer.</summary>
    HitscanBurst,

    /// <summary>Instancia um projétil — Explosive Shot.</summary>
    Projectile,

    /// <summary>Só aplica modificadores temporários. Pós-MVP.</summary>
    SelfBuff,
}
