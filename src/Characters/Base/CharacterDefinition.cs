using Contenda.Components.Abilities;
using Contenda.Components.Health;
using Contenda.Components.Mana;
using Contenda.Components.Movement;
using Contenda.Components.Stats;
using Contenda.Components.Transformations;
using Contenda.Weapons;
using Godot;

namespace Contenda.Characters.Base;

/// <summary>
/// Tudo que diferencia um personagem de outro.
/// </summary>
/// <remarks>
/// **É aqui que a diferença entre Swordsman e Gunslinger vive** — nunca em
/// ramificação de código. Um terceiro arquétipo é um `.tres` novo, não um `if`.
///
/// Carrega combate, apresentação e os ratings usados na seleção do M7.
/// Ver docs/specs/08-personagens.md §1.
/// </remarks>
[GlobalClass]
public sealed partial class CharacterDefinition : Resource
{
    /// <summary>Identificador estável. Saves e estatísticas dependem dele.</summary>
    [Export] public StringName Id { get; set; } = new("sem_id");

    /// <summary>Nome exibido nas telas.</summary>
    [Export] public string DisplayName { get; set; } = "Sem nome";

    /// <summary>Resumo exibido na seleção de personagem.</summary>
    [Export(PropertyHint.MultilineText)] public string Bio { get; set; } = "";

    [Export] public Color ThemeColor { get; set; } = Colors.White;

    /// <summary>Modelo 3D usado na seleção e na arena.</summary>
    [Export] public PackedScene? ModelScene { get; set; }

    [Export] public float ModelScale { get; set; } = 1f;
    [Export] public NodePath ModelSkeletonPath { get; set; } = new();
    [Export] public NodePath ModelBodyMeshPath { get; set; } = new();
    [Export] public CharacterAnimationSet? AnimationSet { get; set; }
    [Export] public StringName WeaponBoneName { get; set; } = new("handslot.r");

    [Export(PropertyHint.Range, "1,5,1")] public int RatingDamage { get; set; } = 3;
    [Export(PropertyHint.Range, "1,5,1")] public int RatingRange { get; set; } = 3;
    [Export(PropertyHint.Range, "1,5,1")] public int RatingSpeed { get; set; } = 3;
    [Export(PropertyHint.Range, "1,5,1")] public int RatingDurability { get; set; } = 3;

    /// <summary>Parâmetros de vida.</summary>
    [Export] public HealthDefinition? Health { get; set; }

    /// <summary>Parâmetros de mana. Nulo para quem não usa mana (inimigo simples).</summary>
    [Export] public ManaDefinition? Mana { get; set; }

    /// <summary>A arma equipada. É ela que decide o que M1 faz.</summary>
    [Export] public WeaponDefinition? Weapon { get; set; }

    /// <summary>Parâmetros de locomoção.</summary>
    [Export] public MovementSettings? Movement { get; set; }

    /// <summary>Chance e multiplicador de crítico. Nulo (inimigos) nunca critica.</summary>
    [Export] public StatsDefinition? Stats { get; set; }

    /// <summary>As habilidades executáveis por sequência de WASD. Vazia é válido — sem AbilityComponent, sem M2.</summary>
    [Export] public AbilityDefinition[] Abilities { get; set; } = [];

    /// <summary>Formas disponíveis para este personagem; vazia é válido.</summary>
    [Export] public TransformationDefinition[] Transformations { get; set; } = [];
}
