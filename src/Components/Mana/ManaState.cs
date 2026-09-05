using System;
using Godot;

namespace Contenda.Components.Mana;

/// <summary>
/// Mana, consumo e regeneração com atraso — sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>HealthState</c>: é a lógica que precisa de
/// teste, e regeneração com atraso é exatamente o tipo de janela de tempo
/// onde erro de comparação passa despercebido.
///
/// Duas formas de gastar, e a distinção não é detalhe — ver ticket 10:
/// <see cref="TryConsume"/> é tudo-ou-nada (habilidades, M3); <see cref="Drain"/>
/// é parcial e nunca falha (dreno de transformação, M4 — o dreno contínuo não
/// pode "falhar", ele esgota e força a reversão).
/// </remarks>
public sealed class ManaState
{
    private float _max;
    private readonly float _regenDelay;
    private readonly float _startingPercent;
    private float _atraso;

    public ManaState(float maxMana, float regenDelayAfterSpend, float startingPercent = 1f)
    {
        _max = Mathf.Max(0f, maxMana);
        _regenDelay = Mathf.Max(0f, regenDelayAfterSpend);
        _startingPercent = Mathf.Clamp(startingPercent, 0f, 1f);
        Current = _max * _startingPercent;
    }

    /// <summary>Mana atual.</summary>
    public float Current { get; private set; }

    /// <summary>Mana máxima.</summary>
    public float Max => _max;

    /// <summary>Fração de mana, de 0 a 1.</summary>
    public float Percent => _max <= 0f ? 0f : Current / _max;

    /// <summary>Avisa que a mana mudou, com o valor atual e o máximo.</summary>
    public event Action<float, float>? ManaChanged;

    /// <summary>Avisa que a mana chegou a zero.</summary>
    public event Action? Depleted;

    /// <summary>Se um <see cref="TryConsume"/> deste valor passaria agora.</summary>
    public bool CanConsume(float quanto) => quanto <= Current;

    /// <summary>
    /// Tenta gastar. Tudo ou nada: sem saldo suficiente, nada muda.
    /// </summary>
    /// <returns>Se o gasto saiu.</returns>
    public bool TryConsume(float quanto)
    {
        if (quanto <= 0f)
            return true;

        if (quanto > Current)
            return false;

        Gastar(quanto);
        return true;
    }

    /// <summary>
    /// Drena o que houver, até <paramref name="quantoPorQuadro"/>. Nunca falha.
    /// </summary>
    /// <returns>Quanto foi de fato consumido.</returns>
    public float Drain(float quantoPorQuadro)
    {
        if (quantoPorQuadro <= 0f)
            return 0f;

        var consumido = Mathf.Min(Current, quantoPorQuadro);
        if (consumido > 0f)
            Gastar(consumido);

        return consumido;
    }

    /// <summary>Devolve mana, sem passar do máximo.</summary>
    public void Restore(float quanto)
    {
        if (quanto <= 0f)
            return;

        var antes = Current;
        Current = Mathf.Min(_max, Current + quanto);

        if (Current > antes)
            ManaChanged?.Invoke(Current, _max);
    }

    /// <summary>
    /// Envelhece a pausa de regeneração e regenera quando ela zera.
    /// </summary>
    /// <param name="delta">Tempo do tique, em segundos.</param>
    /// <param name="regenPerSecond">
    /// Taxa de regeneração agora, em mana por segundo. Passada a cada chamada,
    /// nunca fixada na construção: é assim que <c>ManaRegen</c> do
    /// <c>StatBlock</c> tem efeito sem editar o <c>ManaDefinition</c> — a
    /// mesma escolha do <c>AttackInterval</c> do revólver no ticket 09.
    /// </param>
    public void Advance(float delta, float regenPerSecond)
    {
        if (delta <= 0f)
            return;

        if (_atraso > 0f)
            _atraso = Mathf.Max(0f, _atraso - delta);

        if (_atraso > 0f || regenPerSecond <= 0f || Current >= _max)
            return;

        var antes = Current;
        Current = Mathf.Min(_max, Current + (regenPerSecond * delta));

        if (Current > antes)
            ManaChanged?.Invoke(Current, _max);
    }

    /// <summary>
    /// Altera a mana máxima preservando a FRAÇÃO atual.
    /// </summary>
    /// <remarks>Mesmo motivo do <c>HealthState.SetMax</c>: sem isto, uma
    /// transformação que aumenta o teto encheria ou esvaziaria de graça.
    /// </remarks>
    public void SetMax(float novoMaximo)
    {
        var fracao = Percent;
        _max = Mathf.Max(0f, novoMaximo);
        Current = _max * fracao;
    }

    /// <summary>
    /// Devolve ao estado de recém-criado. Contrato do pool, no M5.
    /// </summary>
    /// <remarks>
    /// "Recém-criado" usa <c>StartingManaPercent</c>, não necessariamente
    /// cheio: um boss que nasce com a mana parcial (balanceamento) tem que
    /// voltar à mesma fração ao ser reciclado pelo pool, não subir para 100%.
    /// </remarks>
    public void Reset()
    {
        Current = _max * _startingPercent;
        _atraso = 0f;
    }

    private void Gastar(float quanto)
    {
        Current -= quanto;
        _atraso = _regenDelay;
        ManaChanged?.Invoke(Current, _max);

        if (Current <= 0f)
            Depleted?.Invoke();
    }
}
