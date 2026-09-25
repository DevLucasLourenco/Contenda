using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>
/// O balanceamento do placar como dado (`.tres`). Spec 10 §8.
/// </summary>
/// <remarks>
/// Balanceamento vive em `.tres`, nunca em `.cs` (CLAUDE.md, regra 4): trocar o
/// combo ou o bônus de onda é editar `data/score/horde_score.tres`. Os valores
/// padrão abaixo são os da spec, para um `Resource` novo já nascer certo.
/// </remarks>
[GlobalClass]
public sealed partial class ScoreRulesDefinition : Resource
{
    [Export(PropertyHint.Range, "0.5,10,0.1")] public float ComboWindowSeconds { get; set; } = 3f;

    [Export(PropertyHint.Range, "0,1,0.05")] public float ComboStep { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "1,10,0.1")] public float ComboCap { get; set; } = 3f;

    [Export(PropertyHint.Range, "0,1,0.05")] public float WaveStep { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "0,2000,10")] public int FlawlessWaveBonus { get; set; } = 250;

    public ScoreRules ToRules() => new(ComboWindowSeconds, ComboStep, ComboCap, WaveStep, FlawlessWaveBonus);
}
