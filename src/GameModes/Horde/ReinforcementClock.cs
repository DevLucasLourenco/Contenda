namespace Contenda.GameModes.Horde;

/// <summary>
/// Decide QUANDO chega o próximo lote de reforços de uma onda de chefe, sem nó
/// nenhum.
/// </summary>
/// <remarks>
/// POCO de propósito, mesma disciplina de <see cref="WaveClearTimer"/>: uma
/// janela de tempo só é testável controlando o relógio fora da engine (spec 15
/// §1). Quem decide SE é hora de contar (o chefe já nasceu e ainda vive) e
/// QUANTOS cabem sob o teto de inimigos é o <c>WaveDirector</c>; este relógio
/// só sabe medir o intervalo. Ver ticket 28, spec 10 §5 ("reforço contínuo").
/// </remarks>
public sealed class ReinforcementClock
{
    private readonly float _interval;
    private float _acumulado;

    public ReinforcementClock(float interval)
    {
        _interval = interval;
    }

    /// <summary>Avança o relógio. Só acumula enquanto <paramref name="active"/>; inativo congela, sem zerar.</summary>
    /// <returns>Se um lote está devido -- continua verdadeiro até <see cref="Consume"/>.</returns>
    public bool Tick(float delta, bool active)
    {
        if (_interval <= 0f)
            return false;

        if (active)
            _acumulado += delta;

        return _acumulado >= _interval;
    }

    /// <summary>Marca o lote devido como entregue e recomeça a contar.</summary>
    public void Consume() => _acumulado = 0f;

    /// <summary>
    /// Avança o relógio e decide, de uma vez, se um lote de <paramref name="batch"/>
    /// reforços deve chegar AGORA: está devido e cabe sob o teto de inimigos.
    /// </summary>
    /// <remarks>
    /// Sem espaço, o lote espera -- o relógio continua vencido e o lote nunca é
    /// descartado. Só um lote entregue reinicia a contagem.
    /// </remarks>
    public bool TryDeliver(float delta, bool active, int activeCount, int batch, int cap)
    {
        if (!Tick(delta, active) || activeCount + batch > cap)
            return false;

        Consume();
        return true;
    }
}
