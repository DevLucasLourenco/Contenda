using Contenda.Components.AI;
using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>Uma onda: quem nasce, em que ritmo, e quanto respiro depois de limpa.</summary>
/// <remarks>
/// Ver docs/specs/10-modos-de-jogo-horde.md §3. Dado, não código -- trocar o
/// `.tres` muda a progressão inteira sem recompilar (ticket 27, critério de
/// aceite da spec §10).
///
/// Deliberadamente sem `IsBossWave`/`MusicOverride`/`EliteChance` (também na
/// spec §3): `MusicOverride` pertence ao ticket 36/M8 (áudio de verdade,
/// `AudioDirector` ainda é um autoload vazio); `IsBossWave` nunca foi
/// necessário -- o `WaveDirector` reconhece o chefe pelo próprio
/// `EnemyDefinition.IsBoss` de uma entrada, sem uma flag duplicada na onda
/// (ticket 28); `EliteChance` chegou a existir aqui, mas nada neste ticket lê ou
/// aplica a chance a um spawn de verdade (virar elite muda
/// `EnemyDefinition.IsElite`, um `Resource` COMPARTILHADO por todo `grunt`
/// -- fazer isso por instância exige um mecanismo de sobreposição que este
/// ticket não constrói). Um campo ajustável no editor sem efeito nenhum é
/// pior que não tê-lo -- mesma regra que
/// <see cref="Contenda.Components.AI.EnemyDefinition"/> já segue para o
/// resto do roster.
/// </remarks>
[GlobalClass]
public sealed partial class WaveDefinition : Resource
{
    /// <summary>Texto do banner ao começar, ex.: "ONDA 3".</summary>
    [Export] public string DisplayName { get; set; } = "ONDA";

    /// <summary>Quem nasce nesta onda, e quantos de cada.</summary>
    [Export] public EnemySpawnEntry[] Entries { get; set; } = [];

    /// <summary>Intervalo entre um spawn e o próximo, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float SpawnInterval { get; set; } = 0.8f;

    /// <summary>Teto de inimigos ativos ao mesmo tempo, nesta onda.</summary>
    [Export(PropertyHint.Range, "1,40,1")] public int MaxConcurrent { get; set; } = 12;

    /// <summary>Respiro depois de limpar a onda, antes da próxima começar, em segundos.</summary>
    [Export(PropertyHint.Range, "0,15,0.1")] public float CompletionDelay { get; set; } = 4f;

    // --- reforço contínuo (ticket 28, spec 10 §5: "reforço contínuo" na onda do chefe) ---

    /// <summary>Quem chega de reforço enquanto o chefe da onda vive. Nulo, sem reforços.</summary>
    [Export] public EnemyDefinition? ReinforcementEnemy { get; set; }

    /// <summary>Quantos reforços chegam por lote.</summary>
    [Export(PropertyHint.Range, "1,10,1")] public int ReinforcementBatch { get; set; } = 2;

    /// <summary>Segundos entre um lote de reforço e o seguinte. Zero desliga os reforços.</summary>
    [Export(PropertyHint.Range, "0,60,0.5")] public float ReinforcementInterval { get; set; }

    /// <summary>Grupo de pontos de spawn de onde os reforços descem (spec 17 §6: "as vielas").</summary>
    [Export] public string ReinforcementSpawnGroup { get; set; } = "";
}
