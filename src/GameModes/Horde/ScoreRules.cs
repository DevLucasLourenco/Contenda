namespace Contenda.GameModes.Horde;

/// <summary>Os números do placar, sem engine -- o que o <see cref="ScoreKeeper"/> de fato recebe.</summary>
/// <param name="ComboWindowSeconds">Um abate dentro deste tempo do anterior continua a sequência.</param>
/// <param name="ComboStep">Quanto o multiplicador sobe por abate seguido.</param>
/// <param name="ComboCap">Teto do multiplicador de combo.</param>
/// <param name="WaveStep">Quanto cada onda avançada soma ao multiplicador de onda.</param>
/// <param name="FlawlessWaveBonus">Bônus por limpar uma onda sem o jogador apanhar.</param>
public readonly record struct ScoreRules(
    float ComboWindowSeconds,
    float ComboStep,
    float ComboCap,
    float WaveStep,
    int FlawlessWaveBonus);
