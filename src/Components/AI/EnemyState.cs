namespace Contenda.Components.AI;

/// <summary>Em que fase da percepção/combate um inimigo está agora.</summary>
/// <remarks>
/// Ver docs/specs/09-inimigos-e-ia.md §2. `Death` fica fora por enquanto: sem
/// reciclagem (ticket 25), um inimigo morto não precisa de um estado próprio
/// — `HealthComponent.Died` já basta para quem precisar reagir.
/// </remarks>
public enum EnemyState : byte
{
    /// <summary>Parado, ignorando o mundo — ainda não percebeu ninguém.</summary>
    Idle,

    /// <summary>Acabou de perceber o alvo; encara antes de partir. Dá tempo de leitura ao jogador.</summary>
    Alert,

    /// <summary>Persegue o alvo até entrar em alcance de ataque.</summary>
    Chase,

    /// <summary>Golpe em preparação ou em andamento.</summary>
    Attack,

    /// <summary>Cooldown pós-golpe, antes de poder atacar de novo.</summary>
    Recover,

    /// <summary>Atordoado por ter apanhado. Sai sozinho, de volta para <see cref="Chase"/>.</summary>
    Staggered,

    /// <summary>
    /// Lançado no ar por um golpe vertical (anti-aéreo ou combo aéreo, ticket
    /// 19). Sem navegação, sem ataque, sujeito só à gravidade -- sai ao
    /// tocar o chão, de volta para <see cref="Staggered"/> (que se recompõe
    /// sozinho, de volta para <see cref="Chase"/>). Ver ticket 24, spec 16 §6.
    /// </summary>
    Airborne,

    /// <summary>
    /// A vida chegou a zero. Entra a partir de qualquer estado, inclusive
    /// <see cref="Airborne"/>, e nunca sai sozinho -- só a reciclagem do pool
    /// (<c>ResetForSpawn</c>) tira daqui, construindo uma máquina nova em
    /// <see cref="Idle"/>. Ver ticket 25, spec 09 §2 e §8.
    /// </summary>
    Death,
}
