using Godot;

namespace Contenda.Core;

/// <summary>
/// Camadas de colisão 3D, como máscara de bits.
/// </summary>
/// <remarks>
/// Espelha exatamente os nomes em Project Settings → Layer Names → 3D Physics.
/// Os dois lados precisam andar juntos: mexeu aqui, mexeu lá.
/// Ver docs/specs/01-arquitetura-tecnica.md §8.
/// </remarks>
public static class PhysicsLayers
{
    public const uint World         = 1u << 0;
    public const uint PlayerBody    = 1u << 1;
    public const uint EnemyBody     = 1u << 2;
    public const uint PlayerHitbox  = 1u << 3;
    public const uint EnemyHitbox   = 1u << 4;
    public const uint PlayerHurtbox = 1u << 5;
    public const uint EnemyHurtbox  = 1u << 6;
    public const uint Projectile    = 1u << 7;
    public const uint GroundPlane   = 1u << 8;
    public const uint Interactable  = 1u << 9;

    /// <summary>Nomes na ordem dos bits, para o teste de drift contra project.godot.</summary>
    public static readonly string[] OrderedNames =
    [
        "World", "PlayerBody", "EnemyBody", "PlayerHitbox", "EnemyHitbox",
        "PlayerHurtbox", "EnemyHurtbox", "Projectile", "GroundPlane", "Interactable",
    ];
}

/// <summary>
/// Máscaras prontas. Existem para que nenhuma cena precise somar bits à mão —
/// um OR errado no editor é invisível até alguém levar dano do próprio time.
/// </summary>
public static class PhysicsMasks
{
    /// <summary>O que um golpe do jogador pode atingir.</summary>
    public const uint PlayerHitscanTargets = PhysicsLayers.EnemyHurtbox;

    /// <summary>O que um golpe de inimigo pode atingir.</summary>
    public const uint EnemyHitscanTargets = PhysicsLayers.PlayerHurtbox;

    /// <summary>Corpos sólidos, para raycast de linha de visão e de mira.</summary>
    public const uint Solid = PhysicsLayers.World;
}

/// <summary>
/// Nomes das ações do InputMap.
/// </summary>
/// <remarks>
/// Sempre <see cref="StringName"/>: as convenções proíbem <c>string</c> em hot
/// path, e input é lido a cada tique de física.
/// Ver docs/specs/03-input-comandos-e-combos.md §1.
/// </remarks>
public static class InputActions
{
    public static readonly StringName MoveUp    = new("move_up");
    public static readonly StringName MoveDown  = new("move_down");
    public static readonly StringName MoveLeft  = new("move_left");
    public static readonly StringName MoveRight = new("move_right");

    public static readonly StringName AttackBasic    = new("attack_basic");
    public static readonly StringName CommandConfirm = new("command_confirm");

    public static readonly StringName FormPrev     = new("form_prev");
    public static readonly StringName FormNext     = new("form_next");
    public static readonly StringName FormActivate = new("form_activate");

    public static readonly StringName Pause = new("pause");
    public static readonly StringName Dodge = new("dodge");

    /// <summary>Para o teste de drift contra project.godot.</summary>
    public static readonly string[] All =
    [
        "move_up", "move_down", "move_left", "move_right",
        "attack_basic", "command_confirm",
        "form_prev", "form_next", "form_activate",
        "pause", "dodge",
    ];
}

/// <summary>Time de uma entidade. Decide quem pode causar dano a quem.</summary>
public enum Team : byte
{
    Neutral = 0,
    Player = 1,
    Enemy = 2,
}
