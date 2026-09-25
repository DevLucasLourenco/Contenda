using Contenda.GameModes.Horde;

namespace Contenda.GameModes;

/// <summary>O que um modo precisa saber para começar. Só o conjunto de ondas, por enquanto.</summary>
/// <param name="WaveSet">A progressão da partida. Trocar o `.tres` troca a partida inteira.</param>
public readonly record struct GameModeConfig(WaveSetDefinition WaveSet);
