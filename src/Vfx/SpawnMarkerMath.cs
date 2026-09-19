namespace Contenda.Vfx;

/// <summary>O crescimento e o desaparecimento do marcador de nascimento no chão, sem nó nenhum.</summary>
/// <remarks>
/// Mesmo desenho de <see cref="FloatingDamageMath"/>: um anel que cresce do
/// pequeno ao tamanho cheio, e só começa a sumir bem perto do fim -- o
/// inimigo aparece logo depois do anel desaparecer, não no meio do
/// crescimento. Ver docs/specs/10-modos-de-jogo-horde.md §6, "efeito de
/// spawn".
/// </remarks>
public static class SpawnMarkerMath
{
    /// <summary>Fração do tempo total, a partir da qual o anel começa a sumir.</summary>
    private const float InicioDoSumico = 0.8f;

    /// <summary>
    /// Escala e opacidade do anel neste instante.
    /// </summary>
    /// <param name="elapsed">Segundos desde que o marcador apareceu.</param>
    /// <param name="duration">Quanto tempo total o marcador vive, em segundos.</param>
    /// <param name="scale">Escala do anel, de 0,3 (recém-aparecido) a 1,0 (cheio).</param>
    /// <param name="alpha">Opacidade, de 1 até <see cref="InicioDoSumico"/> do tempo, depois caindo a 0.</param>
    /// <returns>Falso quando o marcador já deveria ter sumido -- o chamador recicla o slot.</returns>
    public static bool Evaluate(float elapsed, float duration, out float scale, out float alpha)
    {
        if (elapsed >= duration || duration <= 0f)
        {
            scale = 1f;
            alpha = 0f;
            return false;
        }

        var t = elapsed / duration;
        scale = 0.3f + (0.7f * t);
        alpha = t < InicioDoSumico ? 1f : 1f - ((t - InicioDoSumico) / (1f - InicioDoSumico));

        return true;
    }
}
