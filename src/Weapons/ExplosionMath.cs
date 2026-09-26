using System;

namespace Contenda.Weapons;

/// <summary>Queda linear de dano entre o centro e a borda de uma explosão.</summary>
public static class ExplosionMath
{
    public static float DamageMultiplier(float distance, float radius, float edgeMultiplier)
    {
        if (radius <= 0f)
            return 0f;

        var fraction = Math.Clamp(distance / radius, 0f, 1f);
        return 1f + ((Math.Clamp(edgeMultiplier, 0f, 1f) - 1f) * fraction);
    }
}
