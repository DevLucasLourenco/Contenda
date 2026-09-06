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
}
