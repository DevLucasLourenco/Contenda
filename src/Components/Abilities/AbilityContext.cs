using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>O que um <see cref="IAbilityBehavior"/> precisa para resolver o próprio efeito.</summary>
/// <remarks>
/// Criado uma vez por execução, pelo <c>AbilityComponent</c> — nunca pelo
/// comportamento. <see cref="AimDirection"/> vem da mira do personagem quando
/// há uma (<c>TargetingComponent.HasAim</c>), e da frente do corpo quando não
/// há; <see cref="DashAttackBehavior"/> ignora o campo e usa sempre a frente
/// do corpo, mas as habilidades de tiro do ticket 15 (Deadeye, Fan The Hammer)
/// vão precisar dele. Ver spec 05 §3.
/// </remarks>
public sealed class AbilityContext
{
    public AbilityContext(
        CharacterContext character,
        AbilityDefinition definition,
        Vector3 origin,
        Vector3 aimDirection,
        StringName targetGroup)
    {
        Character = character;
        Definition = definition;
        Origin = origin;
        AimDirection = aimDirection;
        TargetGroup = targetGroup;
    }

    /// <summary>O personagem executando a habilidade.</summary>
    public CharacterContext Character { get; }

    /// <summary>Os dados da habilidade em execução.</summary>
    public AbilityDefinition Definition { get; }

    /// <summary>Onde o corpo estava no instante em que a execução começou.</summary>
    public Vector3 Origin { get; }

    /// <summary>Para onde a habilidade mira, capturado no início da execução.</summary>
    public Vector3 AimDirection { get; }

    /// <summary>Grupo varrido em busca de alvos — o mesmo que o <c>CombatComponent</c> usa.</summary>
    public StringName TargetGroup { get; }

    /// <summary>
    /// Há quanto tempo a execução está rodando, em segundos. Atualizado pelo
    /// <c>AbilityComponent</c> antes de cada <see cref="IAbilityBehavior.Tick"/>.
    /// </summary>
    public float ElapsedTime { get; set; }
}
