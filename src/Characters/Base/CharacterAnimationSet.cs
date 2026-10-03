using Godot;

namespace Contenda.Characters.Base;

/// <summary>Mapeia papéis visuais aos clipes de animação de um personagem.</summary>
[GlobalClass]
public sealed partial class CharacterAnimationSet : Resource
{
    [Export] public PackedScene[] AnimationBanks { get; set; } = [];
    [Export] public StringName Idle { get; set; } = new("Idle_A");
    [Export] public StringName Walk { get; set; } = new("Walking_A");
    [Export] public StringName Run { get; set; } = new("Running_A");
    [Export] public StringName[] MeleeAttacks { get; set; } = [];
    [Export] public StringName[] AerialAttacks { get; set; } = [];
    [Export] public StringName Shoot { get; set; } = new("Ranged_1H_Shoot");
    [Export] public StringName Reload { get; set; } = new("Ranged_1H_Reload");
    [Export] public StringName ArmCannonShoot { get; set; } = new("Ranged_2H_Shoot");
    [Export] public StringName Jump { get; set; } = new("Jump_Full_Short");
    [Export] public StringName Fall { get; set; } = new("Jump_Idle");
    [Export] public StringName Land { get; set; } = new("Jump_Land");
    [Export] public StringName Dash { get; set; } = new("Dodge_Forward");
    [Export] public StringName Dive { get; set; } = new("Melee_1H_Attack_Jump_Chop");
    [Export] public StringName Hit { get; set; } = new("Hit_A");
    [Export] public StringName Death { get; set; } = new("Death_A");
    [Export] public StringName Transform { get; set; } = new("EXPERIMENTAL_Medium_Transform");
    [Export] public AbilityAnimationBinding[] AbilityAnimations { get; set; } = [];

    public StringName AnimationForAbility(StringName abilityId)
    {
        foreach (var binding in AbilityAnimations)
        {
            if (binding.AbilityId == abilityId)
                return binding.Animation;
        }

        return new StringName();
    }
}
