using Godot;

namespace Contenda.Components.Health;

/// <summary>
/// Congela um personagem por um instante — sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>ActionLockSet</c>: janela de tempo é onde erro
/// de comparação passa despercebido.
///
/// Vive em <see cref="HealthComponent"/>, não em <c>CombatComponent</c>,
/// porque hitstop tem que congelar os DOIS envolvidos — e um manequim de
/// treino tem vida, mas não tem combate. Quem aplica é a arma, em ambos os
/// lados do golpe (<c>ctx.Health?.ApplyHitstop</c> e
/// <c>alvo.Context?.Health?.ApplyHitstop</c>).
///
/// **Nunca via <c>Engine.TimeScale</c>.** Isso pausaria a horda inteira junto
/// no M5. <see cref="TimeScale"/> é local a QUEM segura esta instância — o
/// <c>CharacterController</c> multiplica o próprio delta por ele antes de
/// repassar a movimento e combate. Ver ticket 11.
/// </remarks>
public sealed class HitstopState
{
    private float _restante;

    /// <summary>Se o congelamento está ativo agora.</summary>
    public bool IsActive => _restante > 0f;

    /// <summary>Multiplicador de delta: 0 congelado, 1 normal.</summary>
    public float TimeScale => IsActive ? 0f : 1f;

    /// <summary>
    /// Pede um congelamento pela duração dada.
    /// </summary>
    /// <remarks>
    /// Toma o MAIOR entre o que resta e o pedido novo — um golpe normal
    /// (0,04 s) chegando no meio do congelamento de um finalizador (0,09 s)
    /// não pode encurtá-lo. Mesma regra do <c>ActionLockSet</c>.
    /// </remarks>
    public void Apply(float duration)
    {
        if (duration > _restante)
            _restante = duration;
    }

    /// <summary>Envelhece o congelamento pelo quadro.</summary>
    public void Advance(float delta)
    {
        if (_restante > 0f)
            _restante = Mathf.Max(0f, _restante - delta);
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void Reset() => _restante = 0f;
}
