using Godot;

namespace Contenda.Components.Abilities;

/// <summary>
/// O relógio de uma execução: quanto tempo passou, e se já deveria ter terminado.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>MeleeCombo</c> e o <c>HitstopState</c>: janela
/// de tempo é onde erro de comparação passa despercebido, e testá-la exige
/// controlar o relógio -- só dá pra fazer fora da engine. Guarda só o próprio
/// avanço; não sabe nada de <c>AbilityDefinition</c>, mana, trava ou recarga --
/// isso é o <c>AbilityComponent</c> que decide, esta classe só responde "já
/// devia ter acabado?" a cada quadro. Ver spec 05 §4 e o ticket 14.
/// </remarks>
public sealed class AbilityTimeline
{
    private float _duracaoTotal;

    /// <summary>Se uma execução está em andamento agora.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Há quanto tempo a execução atual está rodando, em segundos.</summary>
    public float Elapsed { get; private set; }

    /// <summary>Começa a contar, do zero. Chamado no INÍCIO da execução.</summary>
    public void Begin(float castTime, float recoveryTime)
    {
        Elapsed = 0f;
        _duracaoTotal = Mathf.Max(0f, castTime) + Mathf.Max(0f, recoveryTime);
        IsActive = true;
    }

    /// <summary>
    /// Envelhece o relógio pelo quadro.
    /// </summary>
    /// <returns>
    /// Verdadeiro se a execução acabou de terminar NESTE avanço -- ou seja, se
    /// o quadro anterior ainda estava dentro da janela e este a estourou. Um
    /// <see cref="Advance"/> chamado sem execução ativa devolve falso sempre.
    /// </returns>
    public bool Advance(float delta)
    {
        if (!IsActive)
            return false;

        Elapsed += delta;
        if (Elapsed < _duracaoTotal)
            return false;

        IsActive = false;
        return true;
    }

    /// <summary>Interrompe a execução em andamento, sem esperar o relógio estourar.</summary>
    public void Cancel() => IsActive = false;
}
