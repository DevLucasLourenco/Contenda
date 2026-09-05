namespace Contenda.Components.Abilities;

/// <summary>Como o efeito de uma habilidade acontece, uma vez selecionado pelo <see cref="AbilityEffectKind"/>.</summary>
/// <remarks>
/// Cada implementação decide sozinha QUANDO, dentro da própria janela de
/// <see cref="AbilityContext.ElapsedTime"/>, o efeito de fato resolve — o
/// <c>AbilityComponent</c> só avança o relógio e chama <see cref="Tick"/> a
/// cada quadro, sem saber se a habilidade tem um instante de acerto único
/// (dash) ou vários (rajada). Ver spec 05 §3-4.
/// </remarks>
public interface IAbilityBehavior
{
    /// <summary>Chamado uma vez, no instante em que a execução começa.</summary>
    void Begin(AbilityContext ctx);

    /// <summary>Chamado a cada quadro, do início do cast ao fim da recuperação.</summary>
    void Tick(AbilityContext ctx, float delta);

    /// <summary>
    /// Chamado uma vez, ao fim da execução.
    /// </summary>
    /// <param name="ctx">O contexto desta execução.</param>
    /// <param name="cancelled">
    /// Verdadeiro se a execução foi interrompida (morte, atordoamento) antes de
    /// chegar ao fim natural da recuperação — spec 05 §4. Mana não é devolvida
    /// e a recarga não é liberada em nenhum dos dois casos.
    /// </param>
    void End(AbilityContext ctx, bool cancelled);
}
