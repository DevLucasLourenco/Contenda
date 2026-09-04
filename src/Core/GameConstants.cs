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
/// Nomes das ações do InputMap, como texto puro.
/// </summary>
/// <remarks>
/// Esta classe é a lista canônica e **não toca em nenhum tipo do Godot**, de
/// propósito: construir um <see cref="StringName"/> chama código nativo, que não
/// existe fora do editor. Um teste que tocasse em <see cref="InputActions"/>
/// morreria no inicializador de tipo com uma mensagem que não explica nada.
///
/// Ver docs/specs/03-input-comandos-e-combos.md §1.
/// </remarks>
public static class InputActionNames
{
    public const string MoveUp    = "move_up";
    public const string MoveDown  = "move_down";
    public const string MoveLeft  = "move_left";
    public const string MoveRight = "move_right";

    public const string AttackBasic    = "attack_basic";
    public const string CommandConfirm = "command_confirm";

    public const string FormPrev     = "form_prev";
    public const string FormNext     = "form_next";
    public const string FormActivate = "form_activate";

    public const string Pause = "pause";
    public const string Dash  = "dash";
    public const string Jump  = "jump";

    /// <summary>Lista canônica, conferida contra project.godot em teste.</summary>
    public static readonly string[] All =
    [
        MoveUp, MoveDown, MoveLeft, MoveRight,
        AttackBasic, CommandConfirm,
        FormPrev, FormNext, FormActivate,
        Pause, Dash, Jump,
    ];
}

/// <summary>
/// As mesmas ações como <see cref="StringName"/>, para uso em runtime.
/// </summary>
/// <remarks>
/// Input é lido a cada tique de física e as convenções proíbem <c>string</c> em
/// hot path. Só use esta classe dentro do jogo — em teste, use
/// <see cref="InputActionNames"/>.
/// </remarks>
public static class InputActions
{
    public static readonly StringName MoveUp    = new(InputActionNames.MoveUp);
    public static readonly StringName MoveDown  = new(InputActionNames.MoveDown);
    public static readonly StringName MoveLeft  = new(InputActionNames.MoveLeft);
    public static readonly StringName MoveRight = new(InputActionNames.MoveRight);

    public static readonly StringName AttackBasic    = new(InputActionNames.AttackBasic);
    public static readonly StringName CommandConfirm = new(InputActionNames.CommandConfirm);

    public static readonly StringName FormPrev     = new(InputActionNames.FormPrev);
    public static readonly StringName FormNext     = new(InputActionNames.FormNext);
    public static readonly StringName FormActivate = new(InputActionNames.FormActivate);

    public static readonly StringName Pause = new(InputActionNames.Pause);
    public static readonly StringName Dash  = new(InputActionNames.Dash);
    public static readonly StringName Jump  = new(InputActionNames.Jump);
}

/// <summary>Time de uma entidade. Decide quem pode causar dano a quem.</summary>
public enum Team : byte
{
    Neutral = 0,
    Player = 1,
    Enemy = 2,
}
