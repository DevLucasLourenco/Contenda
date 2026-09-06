using Contenda.Components.Stats;
using Contenda.Core;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>Instancia um projétil que voa reto e explode numa área.</summary>
/// <remarks>
/// Explosive Shot — spec 05 §5, "55 dano em 3 m, 18 m de alcance". Só dispara
/// o pedido para o <see cref="ProjectilePool"/> via <see cref="GameEvents"/> —
/// não sabe de <c>Node</c>, malha nem colisão nenhuma, o pool é quem voa de
/// verdade. <see cref="AbilityDefinition.Range"/> vira alcance MÁXIMO (tempo
/// de voo = Range / <see cref="VelocidadeDoProjetil"/>), não uma distância
/// percorrida de uma vez como no <see cref="DashAttackBehavior"/> — aqui o
/// projétil viaja quadro a quadro, e explode antes se encontrar um alvo.
///
/// <see cref="AbilityDefinition.ProjectileScene"/> fica sem uso aqui de
/// propósito: sem arte de verdade até o ticket 34/36, o visual do pool é uma
/// esfera provisória, mesmo padrão do rastro do <c>HitscanWeapon</c>.
/// </remarks>
public sealed class ProjectileBehavior : IAbilityBehavior
{
    /// <summary>Velocidade de voo fixa. A spec não dá um campo próprio para isto.</summary>
    private const float VelocidadeDoProjetil = 20f;

    private bool _resolvido;

    public void Begin(AbilityContext ctx) => _resolvido = false;

    public void Tick(AbilityContext ctx, float delta)
    {
        if (_resolvido || ctx.ElapsedTime < ctx.Definition.CastTime)
            return;

        _resolvido = true;
        Disparar(ctx);
    }

    public void End(AbilityContext ctx, bool cancelled)
    {
    }

    private static void Disparar(AbilityContext ctx)
    {
        var corpo = ctx.Character.Body;
        var direcao = AbilityGeometry.AimOrFlattenedForward(ctx.AimDirection, corpo.GlobalTransform.Basis);

        // Sorteado no disparo, não na explosão: a explosão em área reaproveita
        // este mesmo valor para todo mundo que ela atinge -- mesma disciplina
        // do MeleeArcBehavior. Ticket 18, spec 16 §5.
        var critico = CritMath.RolarNaStats(ctx.Character.Stats);
        var dano = CritMath.AplicarNaStats(
            ctx.Character.Stats,
            ctx.Definition.Damage * (ctx.Character.Stats?.Get(StatId.DamageMultiplier) ?? 1f),
            critico);
        var alcance = Mathf.Max(0.1f, ctx.Definition.Range);

        ServiceLocator.Events.RaiseProjectileFire(new ProjectileFireEvent(
            Origin: corpo.GlobalPosition,
            Direction: direcao,
            Speed: VelocidadeDoProjetil,
            LifeTime: alcance / VelocidadeDoProjetil,
            Damage: dano,
            ExplosionRadius: ctx.Definition.Radius,
            MaxTargets: ctx.Definition.MaxTargets,
            Knockback: ctx.Definition.Knockback,
            SourceId: corpo.GetInstanceId(),
            SourceTag: ctx.Definition.Id.ToString(),
            ShooterTeam: ctx.Character.Team,
            TargetGroup: ctx.TargetGroup,
            IsCritical: critico));
    }
}
