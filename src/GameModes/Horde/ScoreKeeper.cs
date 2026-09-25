using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>
/// O placar do modo horda, sem nó nenhum: pontos por abate, multiplicador de
/// onda, combo por abates seguidos e bônus de onda sem dano. Spec 10 §8.
/// </summary>
/// <remarks>
/// POCO de propósito, mesma disciplina de <see cref="WaveClearTimer"/>: a
/// janela de combo é uma comparação de tempo, e o "apanhar zera tudo" é a regra
/// que faz o placar premiar quem arrisca e acerta em vez de quem fica num canto
/// (ticket 29). O relógio é do CHAMADOR -- quem usa passa o "agora" -- e os
/// números vêm de <see cref="ScoreRules"/> (dado, não constante).
///
/// <c>score += ScoreValue × (1 + WaveStep × onda) × combo</c>. O combo sobe
/// <see cref="ScoreRules.ComboStep"/> por abate que caia dentro da janela do
/// anterior, até o teto; o primeiro abate de uma sequência vale 1,0. Contado por
/// abates seguidos (não por acumulação de floats), para o teto ser exato depois
/// de dezenas de abates.
/// </remarks>
public sealed class ScoreKeeper
{
    private readonly ScoreRules _regras;

    private int _abatesSeguidos;
    private float _ultimoAbate = float.NegativeInfinity;
    private bool _ondaSemDano = true;

    public ScoreKeeper(ScoreRules regras)
    {
        _regras = regras;
    }

    public int Score { get; private set; }

    /// <summary>O multiplicador de combo agora (1,0 sem sequência).</summary>
    public float ComboMultiplier => Mathf.Min(_regras.ComboCap, 1f + (_regras.ComboStep * _abatesSeguidos));

    /// <summary>Registra um abate e devolve os pontos que ele rendeu.</summary>
    public int RegisterKill(float now, int baseValue, int waveIndex)
    {
        _abatesSeguidos = now - _ultimoAbate <= _regras.ComboWindowSeconds ? _abatesSeguidos + 1 : 0;
        _ultimoAbate = now;

        var pontos = Mathf.RoundToInt(baseValue * (1f + (_regras.WaveStep * waveIndex)) * ComboMultiplier);
        Score += pontos;
        return pontos;
    }

    /// <summary>O jogador apanhou: zera o combo e estraga o bônus da onda.</summary>
    public void RegisterPlayerDamaged()
    {
        _abatesSeguidos = 0;
        _ultimoAbate = float.NegativeInfinity;
        _ondaSemDano = false;
    }

    /// <summary>Uma onda começou -- o bônus de "sem apanhar" é por onda.</summary>
    public void StartWave() => _ondaSemDano = true;

    /// <summary>A onda foi limpa. Devolve o bônus (zero se o jogador apanhou nela).</summary>
    public int ClearWave()
    {
        var bonus = _ondaSemDano ? _regras.FlawlessWaveBonus : 0;
        Score += bonus;
        return bonus;
    }

    /// <summary>Zera o combo se a janela passou sem abate novo. Verdadeiro só na hora em que zera de fato.</summary>
    public bool ExpireCombo(float now)
    {
        if (_abatesSeguidos == 0 || now - _ultimoAbate <= _regras.ComboWindowSeconds)
            return false;

        _abatesSeguidos = 0;
        return true;
    }
}
