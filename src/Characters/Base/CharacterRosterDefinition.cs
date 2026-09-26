using Godot;

namespace Contenda.Characters.Base;

/// <summary>Elenco jogável, em ordem de apresentação no menu.</summary>
[GlobalClass]
public sealed partial class CharacterRosterDefinition : Resource
{
    [Export] public CharacterDefinition[] Characters { get; set; } = [];
}
