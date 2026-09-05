using Godot;

namespace Contenda.Weapons;

/// <summary>Geometria pura de um tiro instantâneo, sem nó nem física envolvidos.</summary>
public static class HitscanMath
{
    /// <summary>
    /// Se um alvo está a <paramref name="raioDeAcerto"/> ou menos do segmento
    /// de tiro, e a que distância ao longo dele.
    /// </summary>
    /// <remarks>
    /// O revólver não usa hurtbox: como o combo corpo a corpo do ticket 08, o
    /// acerto é geometria contra a posição do alvo, não colisão de física. O
    /// ponto mais próximo do SEGMENTO (não da reta infinita) é limitado a
    /// <c>[0, comprimento]</c> — um alvo atrás do atirador ou além do alcance
    /// não conta como "no caminho do tiro" só porque a reta passaria perto.
    /// </remarks>
    /// <param name="origem">De onde o tiro parte.</param>
    /// <param name="direcao">Direção do tiro, já normalizada.</param>
    /// <param name="comprimento">Alcance efetivo deste tiro, em metros.</param>
    /// <param name="alvo">Posição do alvo candidato.</param>
    /// <param name="raioDeAcerto">Tolerância lateral para contar como acerto.</param>
    /// <param name="distanciaAoLongoDoRaio">
    /// A que distância de <paramref name="origem"/>, ao longo do raio, fica o
    /// ponto mais próximo do alvo — usado para escolher o alvo mais perto
    /// entre vários candidatos.
    /// </param>
    public static bool TryHitSegment(
        Vector3 origem,
        Vector3 direcao,
        float comprimento,
        Vector3 alvo,
        float raioDeAcerto,
        out float distanciaAoLongoDoRaio)
    {
        var relativo = alvo - origem;
        var t = Mathf.Clamp(relativo.Dot(direcao), 0f, comprimento);
        var pontoMaisProximo = origem + (direcao * t);

        distanciaAoLongoDoRaio = t;
        return alvo.DistanceTo(pontoMaisProximo) <= raioDeAcerto;
    }
}
