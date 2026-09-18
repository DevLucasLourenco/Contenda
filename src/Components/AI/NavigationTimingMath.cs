namespace Contenda.Components.AI;

/// <summary>
/// Decide QUANDO vale a pena recalcular rota, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// Ver docs/specs/09-inimigos-e-ia.md §4: "Repath a cada 0,25 s ou se o alvo
/// andou &gt; 1,5 m." Pedir um novo caminho ao <see cref="Godot.NavigationAgent3D"/>
/// a cada quadro (o alvo, a posição do jogador, muda quase todo quadro numa
/// perseguição de verdade) é o "engasgo visível" que a spec avisa -- com 40
/// inimigos perseguindo ao mesmo tempo, cada um recalculando 60x por segundo,
/// é uma quantidade de trabalho que nenhuma otimização posterior compensa.
/// Separada de <see cref="NavigationMotor"/> pelo mesmo motivo de
/// <c>MovementMath</c>/<c>CritMath</c>: testável fora da engine.
/// </remarks>
public static class NavigationTimingMath
{
    /// <summary>
    /// Se já passou tempo suficiente OU o alvo já andou longe o bastante
    /// desde o último recálculo de verdade.
    /// </summary>
    public static bool ShouldRepath(
        float segundosDesdeOUltimoRepath, float distanciaDesdeOUltimoAlvo, float intervalo, float distanciaMinima)
        => segundosDesdeOUltimoRepath >= intervalo || distanciaDesdeOUltimoAlvo >= distanciaMinima;
}
