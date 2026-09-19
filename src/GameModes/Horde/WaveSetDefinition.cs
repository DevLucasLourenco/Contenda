using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>A progressão inteira de uma partida: a sequência de ondas.</summary>
/// <remarks>
/// Ver docs/specs/10-modos-de-jogo-horde.md §3. Deliberadamente sem
/// `LoopWithScaling`/`ScalingPerLoop` (também na spec §3): horda infinita
/// pós-MVP, não algo que este ticket precisa rodar.
/// </remarks>
[GlobalClass]
public sealed partial class WaveSetDefinition : Resource
{
    /// <summary>Nome do conjunto, ex.: "Horde — Padrão".</summary>
    [Export] public string DisplayName { get; set; } = "Horde";

    /// <summary>As ondas, em ordem.</summary>
    [Export] public WaveDefinition[] Waves { get; set; } = [];
}
