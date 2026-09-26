using Contenda.Weapons;
using Godot;

namespace Contenda.Components.Transformations;

/// <summary>Dados de uma transformação de personagem.</summary>
[GlobalClass]
public sealed partial class TransformationDefinition : Resource
{
    [Export] public StringName Id { get; set; } = new("form.unnamed");
    [Export] public string DisplayName { get; set; } = "Forma";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public Color ThemeColor { get; set; } = Colors.OrangeRed;
    [Export(PropertyHint.Range, "0,100,1")] public float ManaActivationCost { get; set; } = 20f;
    [Export(PropertyHint.Range, "0.1,20,0.1")] public float ManaDrainPerSecond { get; set; } = 4f;
    [Export(PropertyHint.Range, "0,10,0.1")] public float MinimumDuration { get; set; } = 1f;
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float DamageMultiplier { get; set; } = 1.6f;
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float SpeedMultiplier { get; set; } = 1.15f;
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float DefenseMultiplier { get; set; } = 0.8f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float CritChanceBonus { get; set; }
    [Export(PropertyHint.Range, "0,3,1")] public int ExtraAirJumps { get; set; }
    [Export] public WeaponDefinition? WeaponOverride { get; set; }
    [Export] public bool ShowCannonVisual { get; set; }
}
