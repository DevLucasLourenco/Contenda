using Godot;

namespace Contenda.Components.Health;

/// <summary>Natureza do dano. Decide mitigação e efeito visual.</summary>
public enum DamageType : byte
{
    Physical = 0,
    Explosive = 1,

    /// <summary>Ignora mitigação. Reservado para efeitos que não devem ser resistidos.</summary>
    True = 2,
}

/// <summary>
/// Um golpe, com tudo que o alvo precisa saber sobre ele.
/// </summary>
/// <remarks>
/// Struct por valor, e nunca uma referência a nó: o pool de inimigos do M5
/// recicla objetos, e guardar uma referência num evento manteria vivo algo que
/// o pool já considera livre.
/// </remarks>
/// <param name="Amount">Dano bruto, antes da mitigação.</param>
/// <param name="Type">Física, explosiva ou verdadeira.</param>
/// <param name="HitPoint">Onde acertou, para o efeito visual.</param>
/// <param name="Direction">Direção do golpe, para repulsão.</param>
/// <param name="Knockback">Força da repulsão.</param>
/// <param name="SourceId">Identidade de quem golpeou, sem manter o objeto vivo.</param>
/// <param name="SourceTag">O que golpeou: `weapon.sword`, `ability.dash_slash`.</param>
/// <param name="IsCritical">Se saiu crítico. Sorteado por golpe, não por alvo.</param>
public readonly record struct DamageInfo(
    float Amount,
    DamageType Type,
    Vector3 HitPoint,
    Vector3 Direction,
    float Knockback,
    ulong SourceId,
    string SourceTag,
    bool IsCritical);

/// <summary>Quem pode receber dano.</summary>
public interface IDamageable
{
    /// <summary>Enfileira um golpe para ser resolvido no ponto único do quadro.</summary>
    void ApplyDamage(in DamageInfo golpe);

    /// <summary>Se ainda está de pé.</summary>
    bool IsAlive { get; }
}
