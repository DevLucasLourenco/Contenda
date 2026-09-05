using Godot;

namespace Contenda.Weapons;

/// <summary>
/// Distribui o avanço de um golpe ao longo do tempo de preparação.
/// </summary>
/// <remarks>
/// Aplicar o avanço inteiro num quadro só lê como **teleporte**, não como
/// investida — foi o que o primeiro playtest apontou. Espalhá-lo pelo wind-up,
/// entre o clique e a lâmina conectar, faz o personagem deslizar para dentro do
/// golpe, e o movimento passa a parecer parte da animação.
///
/// POCO de propósito: os casos de borda — duração zero, overshoot no último
/// quadro, interrupção no meio — são exatamente o que erra em silêncio.
/// </remarks>
public sealed class LungeMotion
{
    private float _restante;
    private float _porSegundo;

    /// <summary>Se ainda há avanço a aplicar.</summary>
    public bool IsMoving => _restante > 0f;

    /// <summary>Quanto ainda falta percorrer, em metros.</summary>
    public float Remaining => _restante;

    /// <summary>
    /// Começa um avanço.
    /// </summary>
    /// <param name="distancia">Quanto percorrer, em metros.</param>
    /// <param name="duracao">
    /// Em quanto tempo, em segundos. Zero ou menos aplica tudo no primeiro passo
    /// — é o caso de um golpe cuja janela de acerto abre imediatamente.
    /// </param>
    public void Start(float distancia, float duracao)
    {
        if (distancia <= 0f)
        {
            Cancel();
            return;
        }

        _restante = distancia;
        _porSegundo = duracao > 0f ? distancia / duracao : float.PositiveInfinity;
    }

    /// <summary>
    /// Consome e devolve quantos metros avançar neste quadro.
    /// </summary>
    /// <remarks>
    /// Nunca devolve mais do que falta: sem o corte, o último quadro
    /// ultrapassaria a distância pedida, e o excesso cresce com o framerate
    /// baixo — justamente quando menos se quer um solavanco. O corte também é o
    /// que faz a velocidade infinita de <see cref="Start"/> significar "tudo de
    /// uma vez", sem ramo especial.
    ///
    /// O nome não é <c>Step</c> de propósito: neste domínio "passo" já é o
    /// passo do combo (<c>ComboStep</c>, <c>ForwardStep</c>), e
    /// <c>Avancar(_avanco.Step(delta))</c> se leria como a coisa errada.
    /// </remarks>
    public float Consume(float delta)
    {
        if (_restante <= 0f || delta <= 0f)
            return 0f;

        var passo = Mathf.Min(_porSegundo * delta, _restante);
        _restante -= passo;

        return passo;
    }

    /// <summary>Interrompe o avanço. Chamado ao morrer ou tomar atordoamento.</summary>
    public void Cancel()
    {
        _restante = 0f;
        _porSegundo = 0f;
    }
}
