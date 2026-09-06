using Godot;

namespace Contenda.Components.Stats;

/// <summary>
/// Atributos de combate que diferem por personagem.
/// </summary>
/// <remarks>
/// Chance e multiplicador de crítico são **balanceamento**, e balanceamento
/// mora em `.tres`, nunca em `.cs` — regra 4 do CLAUDE.md. É o que faz o
/// espadachim e a pistoleira criticarem com frequências diferentes (spec 16
/// §5) sem um `if` sobre qual personagem está em jogo. Nula (inimigos, por
/// enquanto) equivale a nunca criticar — ver <see cref="StatsComponent.Configure"/>.
/// </remarks>
[GlobalClass]
public sealed partial class StatsDefinition : Resource
{
    /// <summary>Chance de crítico, de 0 a 1.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float CritChance { get; set; }

    /// <summary>Multiplicador do dano num acerto crítico.</summary>
    [Export(PropertyHint.Range, "1,5,0.05")] public float CritMultiplier { get; set; } = 1f;
}
