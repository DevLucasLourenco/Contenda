using Godot;

namespace Contenda.Components.Targeting;

/// <summary>
/// Projeção do cursor no mundo, sem nenhum nó envolvido.
/// </summary>
public static class TargetingMath
{
    private const float Paralelo = 0.0001f;

    /// <summary>
    /// Encontra onde um raio cruza um plano horizontal.
    /// </summary>
    /// <remarks>
    /// O plano fica na **altura do torso**, não em Y=0. Mirando no chão, o erro
    /// cresce com a distância: quanto mais longe o cursor, mais o ponto projetado
    /// se afasta de onde o jogador acha que está apontando.
    ///
    /// Devolve <c>false</c> quando o raio é paralelo ao plano ou aponta para
    /// longe dele — o que acontece de verdade, quando o cursor sobe além do
    /// horizonte. Nesse caso o chamador deve manter a última mira válida, e não
    /// virar o personagem para um ponto inventado.
    /// </remarks>
    public static bool TryProjectToPlane(
        Vector3 origem, Vector3 direcao, float alturaDoPlano, out Vector3 ponto)
    {
        ponto = Vector3.Zero;

        if (Mathf.Abs(direcao.Y) < Paralelo)
            return false;

        var distancia = (alturaDoPlano - origem.Y) / direcao.Y;
        if (distancia < 0f)
            return false;

        ponto = origem + (direcao * distancia);
        return true;
    }

    /// <summary>
    /// Direção horizontal de um ponto a outro.
    /// </summary>
    /// <remarks>
    /// A altura é descartada porque o personagem gira só no plano: um alvo acima
    /// ou abaixo não pode fazê-lo inclinar. Devolve zero quando os pontos
    /// coincidem, para o chamador manter a orientação atual em vez de saltar para
    /// uma direção arbitrária.
    /// </remarks>
    public static Vector3 AimDirection(Vector3 de, Vector3 para)
    {
        var plano = new Vector3(para.X - de.X, 0f, para.Z - de.Z);
        return plano.LengthSquared() < Paralelo ? Vector3.Zero : plano.Normalized();
    }
}
