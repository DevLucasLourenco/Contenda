using System;
using Godot;

namespace Contenda.Weapons;

/// <summary>
/// O que M1 significa, para a arma equipada.
/// </summary>
/// <remarks>
/// **Esta é a abstração do ticket 08.** Antes dela, o <c>CombatComponent</c>
/// era a espada: combo, avanço e varredura de cone viviam embutidos nele, e o
/// <see cref="WeaponDefinition.Kind"/> era um campo que ninguém ramificava —
/// semente do `if (personagem == swordsman)` que a spec 07 §1 proíbe, não a
/// solução.
///
/// Com <c>IWeapon</c>, o <c>CombatComponent</c> não sabe se está segurando
/// espada ou revólver: ele pede o golpe e a implementação decide o resto. É o
/// que permite o revólver hitscan do ticket 09 nascer como uma nova classe em
/// <c>Contenda.Weapons</c>, sem tocar no contêiner.
/// </remarks>
public interface IWeapon
{
    /// <summary>Se há um golpe em andamento.</summary>
    bool IsAttacking { get; }

    /// <summary>Passo atual da cadeia, de 1 a N. Zero quando ocioso ou sem cadeia.</summary>
    int ComboStep { get; }

    /// <summary>Pede um ataque básico.</summary>
    void RequestBasicAttack();

    /// <summary>Avança o estado da arma neste tique de física.</summary>
    void Tick(float delta);

    /// <summary>Interrompe o que estiver em andamento. Chamado ao morrer ou tomar atordoamento.</summary>
    void Cancel();

    /// <summary>Devolve ao estado de recém-criada. Contrato do pool, no M5.</summary>
    void ResetForSpawn();

    /// <summary>Avisa que um golpe começou, com o passo da cadeia (1 quando não há cadeia).</summary>
    event Action<int>? AttackStarted;

    /// <summary>Avisa que um golpe conectou.</summary>
    event Action<Node3D>? HitLanded;
}
