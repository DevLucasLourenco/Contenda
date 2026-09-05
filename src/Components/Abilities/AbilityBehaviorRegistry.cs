using System;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>
/// Decide qual <see cref="IAbilityBehavior"/> um <see cref="AbilityEffectKind"/> produz.
/// </summary>
/// <remarks>
/// Único lugar do projeto que ramifica em <see cref="AbilityEffectKind"/>,
/// mesmo desenho da <c>WeaponFactory</c> do ticket 08: o <c>AbilityComponent</c>
/// não sabe o que a habilidade faz, só que ela faz alguma coisa. Adicionar um
/// <c>Kind</c> novo é adicionar uma entrada aqui — nunca tocar no componente.
/// Ver spec 05 §3.
/// </remarks>
public static class AbilityBehaviorRegistry
{
    public static IAbilityBehavior Criar(AbilityEffectKind kind) => kind switch
    {
        AbilityEffectKind.DashAttack => new DashAttackBehavior(),
        _ => SemImplementacao(kind),
    };

    /// <remarks>
    /// Erro de CONTEÚDO — um `.tres` pedindo um <see cref="AbilityEffectKind"/>
    /// sem implementação — falha alto no boot em debug e só loga em release,
    /// convenções §9: quem está editando o `.tres` deveria descobrir na hora.
    /// </remarks>
    private static IAbilityBehavior SemImplementacao(AbilityEffectKind kind)
    {
        var mensagem = $"AbilityBehaviorRegistry: {kind} ainda não tem IAbilityBehavior (ticket 15). "
                       + "Habilidade fica sem efeito.";

        if (OS.IsDebugBuild())
            throw new NotSupportedException(mensagem);

        GD.PushError(mensagem);
        return NullAbilityBehavior.Instance;
    }
}
