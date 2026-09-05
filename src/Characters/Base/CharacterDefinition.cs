using Contenda.Components.Abilities;
using Contenda.Components.Health;
using Contenda.Components.Mana;
using Contenda.Components.Movement;
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
/// Por enquanto só carrega locomoção; vida, mana, arma, habilidades e
/// transformações entram do M2 em diante, conforme os componentes existirem.
/// Ver docs/specs/08-personagens.md §1.
/// </remarks>
[GlobalClass]
public sealed partial class CharacterDefinition : Resource
{
    /// <summary>Identificador estável. Saves e estatísticas dependem dele.</summary>
    [Export] public StringName Id { get; set; } = new("sem_id");

    /// <summary>Nome exibido nas telas.</summary>
    [Export] public string DisplayName { get; set; } = "Sem nome";

    /// <summary>Parâmetros de vida.</summary>
    [Export] public HealthDefinition? Health { get; set; }

    /// <summary>Parâmetros de mana. Nulo para quem não usa mana (inimigo simples).</summary>
    [Export] public ManaDefinition? Mana { get; set; }

    /// <summary>A arma equipada. É ela que decide o que M1 faz.</summary>
    [Export] public WeaponDefinition? Weapon { get; set; }

    /// <summary>Parâmetros de locomoção.</summary>
    [Export] public MovementSettings? Movement { get; set; }

    /// <summary>As habilidades executáveis por sequência de WASD. Vazia é válido — sem AbilityComponent, sem M2.</summary>
    [Export] public AbilityDefinition[] Abilities { get; set; } = [];
}
