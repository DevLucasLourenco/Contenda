using Contenda.Components.Health;
using Contenda.Components.Stats;
using Contenda.Weapons;

namespace Contenda.Components.Abilities;

/// <summary>Raycast instantâneo que atravessa todo mundo no caminho.</summary>
/// <remarks>
/// Deadeye — spec 05 §5, "120 dano perfurante". "Perfurante" é a única
/// diferença real para o <c>HitscanWeapon</c> do revólver: aquele para no
/// primeiro alvo no caminho, este acerta TODOS dentro do alcance, sem parar no
/// mais próximo. Mira do mouse quando existe, senão a frente do corpo — ao
/// contrário do <see cref="DashAttackBehavior"/>, aqui faz sentido usar
/// <see cref="AbilityContext.AimDirection"/>: é uma habilidade de longa
/// distância da pistoleira, não um golpe corpo a corpo.
/// </remarks>
public sealed class HitscanShotBehavior : IAbilityBehavior
{
    private bool _resolvido;

    public void Begin(AbilityContext ctx) => _resolvido = false;

    public void Tick(AbilityContext ctx, float delta)
    {
        if (_resolvido || ctx.ElapsedTime < ctx.Definition.CastTime)
            return;

        _resolvido = true;
        Executar(ctx);
    }

    public void End(AbilityContext ctx, bool cancelled)
    {
    }

    private static void Executar(AbilityContext ctx)
    {
        var corpo = ctx.Character.Body;
        var origem = corpo.GlobalPosition;
        var direcao = AbilityGeometry.AimOrFlattenedForward(ctx.AimDirection, corpo.GlobalTransform.Basis);
        var dano = ctx.Definition.Damage * (ctx.Character.Stats?.Get(StatId.DamageMultiplier) ?? 1f);
        var atingidos = 0;

        AbilityTargeting.ForEachValidTarget(corpo.GetTree(), ctx.TargetGroup, corpo, ctx.Character.Team, alvo =>
        {
            if (!HitscanMath.TryHitSegment(origem, direcao, ctx.Definition.Range, alvo.GlobalPosition, ctx.Definition.Radius, out _))
                return true;

            // Sem `return false` no primeiro acerto: perfurante significa que
            // a varredura segue e verifica todo o resto do grupo também.
            // `!`: AbilityTargeting só chama este callback para alvos com
            // Health vivo -- é a própria checagem que filtra o candidato.
            alvo.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: dano,
                Type: DamageType.Physical,
                HitPoint: alvo.GlobalPosition,
                Direction: direcao,
                Knockback: ctx.Definition.Knockback,
                SourceId: corpo.GetInstanceId(),
                SourceTag: ctx.Definition.Id.ToString(),
                IsCritical: false));

            alvo.Context.Movement?.ApplyKnockback(direcao * ctx.Definition.Knockback);
            atingidos++;

            return ctx.Definition.MaxTargets <= 0 || atingidos < ctx.Definition.MaxTargets;
        });
    }
}
