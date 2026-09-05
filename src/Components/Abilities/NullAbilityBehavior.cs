namespace Contenda.Components.Abilities;

/// <summary>
/// Habilidade sem efeito nenhum.
/// </summary>
/// <remarks>
/// Devolvida pela <see cref="AbilityBehaviorRegistry"/> para um
/// <see cref="AbilityEffectKind"/> sem <see cref="IAbilityBehavior"/> ainda
/// implementado — todos, exceto <see cref="AbilityEffectKind.DashAttack"/>,
/// até o ticket 15. Existe para o <c>AbilityComponent</c> nunca segurar uma
/// referência nula: null object, mesma escolha do <c>NullWeapon</c>.
/// </remarks>
public sealed class NullAbilityBehavior : IAbilityBehavior
{
    /// <summary>Instância única; não há estado para justificar mais de uma.</summary>
    public static readonly NullAbilityBehavior Instance = new();

    private NullAbilityBehavior()
    {
    }

    public void Begin(AbilityContext ctx)
    {
    }

    public void Tick(AbilityContext ctx, float delta)
    {
    }

    public void End(AbilityContext ctx, bool cancelled)
    {
    }
}
