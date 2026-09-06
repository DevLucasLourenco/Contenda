namespace Contenda.Weapons;

/// <summary>Decisões puras do combate aéreo do corpo a corpo — ticket 19, spec 16 §6.</summary>
public static class AerialCombatMath
{
    /// <summary>
    /// Se a estocada de queda deveria começar agora.
    /// </summary>
    /// <remarks>
    /// "Depois do pico" é <paramref name="velocidadeY"/> ≤ 0: a subida do pulo
    /// é sempre positiva (<c>MovementMath.JumpVelocity</c> nunca devolve
    /// negativo), então o cruzamento por zero é exatamente o instante em que
    /// o pulo vira queda.
    /// </remarks>
    /// <param name="noAr">Se o personagem não está apoiado no chão.</param>
    /// <param name="velocidadeY">Velocidade vertical atual.</param>
    /// <param name="botaoSegurado">Se o botão de ataque básico segue pressionado agora.</param>
    /// <param name="emAtaque">Se algum golpe (solo ou aéreo) já está em andamento.</param>
    /// <param name="jaMergulhando">Se a estocada já está em andamento.</param>
    public static bool DeveComecarMergulho(bool noAr, float velocidadeY, bool botaoSegurado, bool emAtaque, bool jaMergulhando)
        => noAr && velocidadeY <= 0f && botaoSegurado && !emAtaque && !jaMergulhando;
}
