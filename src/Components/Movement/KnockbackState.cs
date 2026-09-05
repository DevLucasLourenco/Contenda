using Godot;

namespace Contenda.Components.Movement;

/// <summary>
/// Repulsão que decai numa duração fixa — sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>LungeMotion</c>: a velocidade de decaimento é
/// fixada UMA VEZ em cada <see cref="Apply"/>, a partir do impulso total NAQUELE
/// instante, e nunca recalculada a cada <see cref="Advance"/> a partir do que
/// resta. Recalcular a cada quadro produz uma curva exponencial que se
/// aproxima de zero sem nunca chegar lá de verdade — mesmo bug que o
/// <c>DamageLayerState</c> evitou no ticket 12. É o que garante que um golpe
/// forte e um fraco decaiam na MESMA duração, só com magnitudes diferentes.
/// </remarks>
public sealed class KnockbackState
{
    private readonly float _duration;
    private Vector3 _porSegundo;
    private bool _instantaneo;

    public KnockbackState(float duration)
    {
        _duration = Mathf.Max(0f, duration);
    }

    /// <summary>A repulsão a somar à velocidade do personagem agora.</summary>
    public Vector3 Current { get; private set; }

    /// <summary>
    /// Aplica um impulso, somando ao que já existe.
    /// </summary>
    /// <remarks>
    /// Somar, não substituir: golpes em sequência (o combo de três, por
    /// exemplo) empurram cada vez mais, em vez de o último apagar os
    /// anteriores. A janela de decaimento reinicia pelo total combinado.
    ///
    /// Duração zero não usa velocidade "infinita" por eixo: um componente
    /// exatamente zero do impulso (0 × ∞) viraria <c>NaN</c> e contaminaria os
    /// outros eixos na subtração de <see cref="Advance"/>. Em vez disso,
    /// duração zero marca decaimento instantâneo explicitamente.
    /// </remarks>
    public void Apply(Vector3 impulso)
    {
        Current += impulso;
        _instantaneo = _duration <= 0f;
        _porSegundo = _instantaneo ? Vector3.Zero : Current / _duration;
    }

    /// <summary>Decai a repulsão pelo quadro, sem ultrapassar zero.</summary>
    public void Advance(float delta)
    {
        if (Current.LengthSquared() <= 0f || delta <= 0f)
            return;

        if (_instantaneo)
        {
            Current = Vector3.Zero;
            return;
        }

        var passo = _porSegundo * delta;

        // Corta no zero em vez de deixar o passo inverter a direção -- sem
        // isso, um delta grande (framerate baixo) faria a repulsão "ricochetear"
        // para o lado oposto em vez de simplesmente parar.
        Current = passo.LengthSquared() >= Current.LengthSquared() ? Vector3.Zero : Current - passo;
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void Reset()
    {
        Current = Vector3.Zero;
        _porSegundo = Vector3.Zero;
        _instantaneo = false;
    }
}
