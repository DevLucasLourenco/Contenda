using Godot;

namespace Contenda.Characters.Base;

[GlobalClass]
public partial class AbilityAnimationBinding : Resource
{
    [Export] public StringName AbilityId { get; set; } = new();
    [Export] public StringName Animation { get; set; } = new();
}
