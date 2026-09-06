using Godot;

namespace Contenda.Components.Movement;

/// <summary>
/// O avanço do dash: direção travada, velocidade constante, por uma duração fixa.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>LungeMotion</c> do ataque corpo a corpo — a
/// mesma classe de bug (duração zero, muitos quadros pequenos, direção
/// mudando no meio) já apareceu antes neste projeto e é exatamente o que vale
/// testar isolado. Velocidade CONSTANTE ao longo da duração inteira, não
/// distribuída como o <c>LungeMotion</c>: o dash da spec 16 §4 é um
/// deslocamento rígido (5 m em 0,18 s), sem wind-up para suavizar — a
/// velocidade nasce pronta e não muda até acabar.
/// </remarks>
public sealed class DashState
{
    private float _restante;
    private Vector3 _direcao;
    private float _velocidadeEscalar;

    /// <summary>Se um dash está em andamento agora.</summary>
    public bool IsActive => _restante > 0f;

    /// <summary>
    /// A velocidade do dash agora — direção travada vezes a velocidade constante.
    /// Zero quando não há dash em andamento.
    /// </summary>
    public Vector3 Velocity => IsActive ? _direcao * _velocidadeEscalar : Vector3.Zero;

    /// <summary>
    /// Começa um dash.
    /// </summary>
    /// <param name="direcao">Para onde avançar. Esperada já normalizada.</param>
    /// <param name="distancia">Quanto percorrer, em metros.</param>
    /// <param name="duracao">Em quanto tempo, em segundos.</param>
    /// <remarks>Distância ou duração não-positivas não iniciam nada — mesma defesa do <c>LungeMotion</c>.</remarks>
    public void Start(Vector3 direcao, float distancia, float duracao)
    {
        if (distancia <= 0f || duracao <= 0f)
        {
            Cancel();
            return;
        }

        _direcao = direcao;
        _velocidadeEscalar = distancia / duracao;
        _restante = duracao;
    }

    /// <summary>Envelhece o dash em andamento pelo quadro.</summary>
    public void Advance(float delta)
    {
        if (_restante <= 0f)
            return;

        _restante = Mathf.Max(0f, _restante - delta);
    }

    /// <summary>Interrompe o dash em andamento.</summary>
    public void Cancel()
    {
        _restante = 0f;
        _velocidadeEscalar = 0f;
        _direcao = Vector3.Zero;
    }
}
