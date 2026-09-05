using Godot;

namespace Contenda.Components.Abilities;

/// <summary>Geometria pura compartilhada pelos <see cref="IAbilityBehavior"/>, sem nó nenhum envolvido.</summary>
/// <remarks>
/// "Frente achatada" e "mira com fallback para a frente" apareciam repetidas,
/// quase idênticas, em <c>DashAttackBehavior</c>, <c>MeleeArcBehavior</c>,
/// <c>UppercutBehavior</c>, <c>HitscanShotBehavior</c>,
/// <c>HitscanBurstBehavior</c> e <c>ProjectileBehavior</c> — seis cópias no
/// mesmo ticket já passa do que vale duplicar. <c>Vector3</c>/<c>Basis</c> são
/// structs puras, então isto é testável em xUnit como o <c>HitscanMath</c>,
/// ao contrário da varredura por alvos (<see cref="AbilityTargeting"/>), que
/// depende de <c>SceneTree</c>.
/// </remarks>
public static class AbilityGeometry
{
    /// <summary>A frente de um corpo, achatada no plano horizontal e normalizada.</summary>
    public static Vector3 FlattenedForward(Basis corpo)
    {
        var bruta = -corpo.Z;
        return new Vector3(bruta.X, 0f, bruta.Z).Normalized();
    }

    /// <summary>
    /// Direção de mira achatada, com fallback para a frente do corpo quando não há mira válida.
    /// </summary>
    /// <remarks>
    /// Usado pelas habilidades de longa distância da pistoleira: sem isto, uma
    /// mira degenerada (ainda sem projeção válida, ou exatamente em cima do
    /// personagem) devolveria um vetor de comprimento zero, e normalizar isso
    /// dá <c>NaN</c>.
    /// </remarks>
    public static Vector3 AimOrFlattenedForward(Vector3 aimDirection, Basis corpo)
    {
        var bruta = aimDirection.LengthSquared() > 0.0001f ? aimDirection : -corpo.Z;
        return new Vector3(bruta.X, 0f, bruta.Z).Normalized();
    }
}
