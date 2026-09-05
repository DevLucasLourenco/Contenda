using System;

namespace Contenda.Components.Combat;

/// <summary>O que uma trava impede o personagem de fazer.</summary>
/// <remarks>
/// Flags, e não um enum simples: um golpe pode travar movimento e rotação ao
/// mesmo tempo, e um atordoamento pode somar ainda mais em cima. Ver spec 07
/// §8 e <see cref="ActionLockSet"/>.
/// </remarks>
[Flags]
public enum ActionLock : byte
{
    None = 0,
    Movement = 1,
    Rotation = 2,
    BasicAttack = 4,
    Abilities = 8,
    Forms = 16,
    All = Movement | Rotation | BasicAttack | Abilities | Forms,
}
