namespace Contenda.Components.Health;

/// <summary>
/// Contador de acertos aéreos consecutivos sofridos por um alvo — teto de
/// impulso vertical antes que ele "caia" de um combo aéreo. Ticket 19, spec
/// 16 §6.
/// </summary>
/// <remarks>
/// POCO de propósito, mesma disciplina do <c>HitstopState</c>/<c>KnockbackState</c>:
/// o teto é onde um erro de comparação passa despercebido.
///
/// Zera quando o alvo toca o chão (chamado por <c>MovementComponent</c>, não
/// por tempo): um combo aéreo de verdade sempre termina em aterrissagem, e
/// contar "quantos acertos desde a última vez que tocou o chão" já é
/// exatamente o que o teto de juggle quer dizer — sem precisar de um
/// temporizador de decaimento à parte.
/// </remarks>
public sealed class AerialJuggleState
{
    /// <summary>Quantos acertos aéreos consecutivos um alvo aguenta antes de os impulsos pararem.</summary>
    public const int LimiteDeAcertos = 4;

    private int _acertos;

    /// <summary>Quantos acertos aéreos consecutivos já foram registrados.</summary>
    public int Acertos => _acertos;

    /// <summary>
    /// Registra mais um acerto aéreo.
    /// </summary>
    /// <returns>Se ainda está sob o teto — quem golpeou só deveria empurrar para cima enquanto isto for true.</returns>
    public bool RegistrarAcerto()
    {
        _acertos++;
        return _acertos <= LimiteDeAcertos;
    }

    /// <summary>Devolve ao estado de recém-criado.</summary>
    public void Reset() => _acertos = 0;
}
