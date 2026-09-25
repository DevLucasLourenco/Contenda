using Contenda.Components.AI;
using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>
/// Um grupo de inimigos dentro de uma onda: qual tipo, quantos, e como
/// entram na ordem de spawn.
/// </summary>
/// <remarks>Ver docs/specs/10-modos-de-jogo-horde.md §3.</remarks>
[GlobalClass]
public sealed partial class EnemySpawnEntry : Resource
{
    /// <summary>Qual inimigo nasce.</summary>
    [Export] public EnemyDefinition? Enemy { get; set; }

    /// <summary>Quantos desta entrada nascem na onda.</summary>
    [Export(PropertyHint.Range, "1,60,1")] public int Count { get; set; } = 5;

    /// <summary>
    /// Peso na ordem de spawn -- entradas com peso maior aparecem
    /// proporcionalmente mais cedo/mais vezes entre as trocas de tipo.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,10,0.1")] public float Weight { get; set; } = 1f;

    /// <summary>Atraso antes do primeiro spawn desta entrada, em segundos.</summary>
    [Export(PropertyHint.Range, "0,30,0.1")] public float DelayBeforeFirst { get; set; }

    /// <summary>
    /// Grupo de nós dos pontos de spawn onde esta entrada nasce (ex.:
    /// `spawn_plaza`). Vazio, qualquer ponto. Ticket 28, spec 17 §6.
    /// </summary>
    [Export] public string SpawnGroup { get; set; } = "";
}
