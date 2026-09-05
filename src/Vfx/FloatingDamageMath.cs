namespace Contenda.Vfx;

/// <summary>A subida e o desaparecimento de um número de dano flutuante, sem nó nenhum.</summary>
public static class FloatingDamageMath
{
    /// <summary>
    /// Onde o número deveria estar e quão opaco, neste instante.
    /// </summary>
    /// <param name="elapsed">Segundos desde que o número apareceu.</param>
    /// <param name="lifetime">Quanto tempo o número vive, em segundos.</param>
    /// <param name="riseHeight">Quanto sobe no total, em metros.</param>
    /// <param name="heightOffset">Deslocamento vertical acumulado até agora.</param>
    /// <param name="alpha">Opacidade, de 1 (recém-aparecido) a 0 (sumindo).</param>
    /// <returns>Falso quando o número já deveria ter sumido — o chamador recicla o slot.</returns>
    public static bool Evaluate(float elapsed, float lifetime, float riseHeight, out float heightOffset, out float alpha)
    {
        if (elapsed >= lifetime || lifetime <= 0f)
        {
            heightOffset = riseHeight;
            alpha = 0f;
            return false;
        }

        var t = elapsed / lifetime;
        heightOffset = riseHeight * t;
        alpha = 1f - t;
        return true;
    }
}
