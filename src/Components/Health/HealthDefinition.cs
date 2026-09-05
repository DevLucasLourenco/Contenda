using Godot;

namespace Contenda.Components.Health;

/// <summary>
/// Parâmetros de vida de uma entidade.
/// </summary>
/// <remarks>
/// Vida máxima é **balanceamento**, e balanceamento mora em `.tres`, nunca em
/// `.cs` nem cravado numa cena — regra 4 do CLAUDE.md. Ter isto aqui é o que
/// permite ajustar a resistência de um inimigo sem recompilar e sem abrir o
/// editor de cenas.
/// </remarks>
[GlobalClass]
public sealed partial class HealthDefinition : Resource
{
    /// <summary>Vida máxima inicial.</summary>
    [Export(PropertyHint.Range, "1,2000,1")] public float MaxHealth { get; set; } = 100f;

    /// <summary>
    /// Invulnerabilidade após apanhar, em segundos. Zero desliga.
    /// </summary>
    /// <remarks>
    /// A spec 04 §2 dá 0,25 s ao jogador e **zero** aos inimigos: i-frames em
    /// inimigo tornariam o modo horda uma sequência de golpes engolidos. O
    /// manequim de treino é a exceção deliberada — ele existe para demonstrar
    /// que a invulnerabilidade funciona.
    /// </remarks>
    [Export(PropertyHint.Range, "0,2,0.05")] public float InvulnerabilityAfterHit { get; set; } = 0.25f;
}
