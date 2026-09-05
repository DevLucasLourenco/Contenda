using System;
using Godot;

namespace Contenda.Weapons;

/// <summary>
/// Arma sem comportamento nenhum.
/// </summary>
/// <remarks>
/// Devolvida pela <see cref="WeaponFactory"/> para um <see cref="WeaponKind"/>
/// sem <see cref="IWeapon"/> ainda implementado — hoje, só
/// <see cref="WeaponKind.Hitscan"/>, que é o ticket 09. Existe para o
/// <c>CombatComponent</c> nunca segurar uma referência nula: null object, não
/// exceção nem `if` de mais um caso especial no contêiner.
/// </remarks>
public sealed class NullWeapon : IWeapon
{
    /// <summary>Instância única; não há estado para justificar mais de uma.</summary>
    public static readonly NullWeapon Instance = new();

    private NullWeapon()
    {
    }

    public bool IsAttacking => false;
    public int ComboStep => 0;

    public event Action<int>? AttackStarted { add { } remove { } }
    public event Action<Node3D>? HitLanded { add { } remove { } }

    public void RequestBasicAttack()
    {
    }

    public void Tick(float delta)
    {
    }

    public void Cancel()
    {
    }

    public void ResetForSpawn()
    {
    }
}
