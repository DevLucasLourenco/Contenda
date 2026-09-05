using System;
using Godot;

namespace Contenda.Components.Health;

/// <summary>
/// Vida, mitigação e morte — sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito: é a lógica que precisa de teste, e a spec 15 §1 proíbe
/// herdar de <c>Node</c> justamente quando se quer testar.
/// O <c>HealthComponent</c> é a casca fina por cima disto.
/// </remarks>
public sealed class HealthState
{
    /// <summary>
    /// Piso da defesa no cálculo de mitigação.
    /// </summary>
    /// <remarks>
    /// Sem piso, defesa zero divide por zero e um golpe de 1 de dano mata
    /// qualquer coisa. Acontece de verdade: basta um modificador zerar a defesa.
    /// </remarks>
    private const float DefesaMinima = 0.1f;

    private float _max;

    public HealthState(float maxHealth)
    {
        _max = Mathf.Max(1f, maxHealth);
        Current = _max;
    }

    /// <summary>Vida atual.</summary>
    public float Current { get; private set; }

    /// <summary>Vida máxima.</summary>
    public float Max => _max;

    /// <summary>Fração de vida, de 0 a 1.</summary>
    public float Percent => _max <= 0f ? 0f : Current / _max;

    /// <summary>Se ainda está de pé.</summary>
    public bool IsAlive { get; private set; } = true;

    /// <summary>Enquanto verdadeiro, ignora todo golpe.</summary>
    public bool IsInvulnerable { get; set; }

    /// <summary>Avisa que apanhou, com o golpe que causou.</summary>
    public event Action<DamageInfo>? Damaged;

    /// <summary>Avisa que caiu. Dispara no máximo uma vez por vida.</summary>
    public event Action<DamageInfo>? Died;

    /// <summary>Avisa que foi curado.</summary>
    public event Action<float>? Healed;

    /// <summary>
    /// Aplica um golpe.
    /// </summary>
    /// <remarks>
    /// **A morte é idempotente.** Já morto, o golpe é descartado sem avisar de
    /// novo: dois golpes no mesmo quadro disparando morte duas vezes é um dos
    /// bugs previsíveis da spec 15 §5, e no M6 significaria pontuar o mesmo
    /// abate duas vezes.
    /// </remarks>
    /// <returns>Se o golpe chegou a ser aplicado.</returns>
    public bool Apply(in DamageInfo golpe, float defesa)
    {
        if (!IsAlive || IsInvulnerable)
            return false;

        var efetivo = golpe.Type == DamageType.True
            ? golpe.Amount
            : golpe.Amount / Mathf.Max(DefesaMinima, defesa);

        Current = Mathf.Max(0f, Current - efetivo);
        Damaged?.Invoke(golpe);

        if (Current > 0f)
            return true;

        IsAlive = false;
        Died?.Invoke(golpe);
        return true;
    }

    /// <summary>Cura, sem passar do máximo e sem ressuscitar.</summary>
    public void Heal(float quanto)
    {
        if (!IsAlive || quanto <= 0f)
            return;

        var antes = Current;
        Current = Mathf.Min(_max, Current + quanto);

        if (Current > antes)
            Healed?.Invoke(Current - antes);
    }

    /// <summary>
    /// Altera a vida máxima preservando a FRAÇÃO de vida.
    /// </summary>
    /// <remarks>
    /// Sem isto, entrar numa transformação que aumenta a vida máxima curaria de
    /// graça, e sair dela mataria. A fração é o que o jogador enxerga na barra,
    /// então é ela que tem que ficar estável.
    /// </remarks>
    public void SetMax(float novoMaximo)
    {
        var fracao = Percent;
        _max = Mathf.Max(1f, novoMaximo);

        if (IsAlive)
            Current = _max * fracao;
    }

    /// <summary>
    /// Devolve ao estado de recém-criado.
    /// </summary>
    /// <remarks>Contrato de reciclagem do pool de inimigos, no M5.</remarks>
    public void Reset()
    {
        Current = _max;
        IsAlive = true;
        IsInvulnerable = false;
    }
}
