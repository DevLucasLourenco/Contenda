namespace Contenda.Weapons;

/// <summary>Como a arma entrega o golpe.</summary>
public enum WeaponKind : byte
{
    /// <summary>Corpo a corpo, com área de dano em janela.</summary>
    Melee = 0,

    /// <summary>Tiro instantâneo por raio. Ticket 09.</summary>
    Hitscan = 1,
}
