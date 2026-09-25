using System.Collections.Generic;

namespace Contenda.Persistence;

/// <summary>O melhor resultado de um personagem. Spec 14 §3.</summary>
public readonly record struct BestResult(int Score, int Waves, int TimeSeconds);

/// <summary>O perfil do jogador: estatísticas somadas e o melhor resultado de cada personagem.</summary>
public sealed class ProfileData
{
    public int TotalMatches { get; set; }

    public int TotalKills { get; set; }

    public int TotalPlaytimeSeconds { get; set; }

    /// <summary>Melhor resultado por id de personagem.</summary>
    public Dictionary<string, BestResult> Best { get; } = new();
}
