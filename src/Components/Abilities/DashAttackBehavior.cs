using Contenda.Components.Health;
using Contenda.Components.Stats;
using Contenda.Weapons;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>
/// Desloca o personagem para a frente e aplica dano a quem estiver no caminho.
/// </summary>
/// <remarks>
/// Dash Slash e Heavy Lunge — spec 05 §5. O <see cref="AbilityDefinition.CastTime"/>
/// do MVP é curto demais (0,10 s) para justificar espalhar o deslocamento por
/// vários quadros como o <c>LungeMotion</c> do ataque básico faz: aqui o dash
/// inteiro resolve num <c>MoveAndCollide</c> só, no instante em que o cast
/// termina — spec 05 §4 chama esse instante de "ativo".
///
/// Sempre na frente do CORPO, nunca na mira do mouse: um golpe corpo a corpo
/// que virasse a direção do dash pela mira ficaria estranho de sentir, e nada
/// no catálogo do MVP pede isso.
///
/// O acerto varre até <c>Max(DashDistance, Range)</c>, não só a distância
/// realmente percorrida: para Dash Slash e Heavy Lunge os dois campos batem
/// (o alcance do golpe é o próprio avanço), mas o Quick Step Shot da
/// pistoleira (spec 05 §5: "dash 4 m, tiro 14 m") é o mesmo <c>Kind</c> com um
/// <c>Range</c> bem maior que o <c>DashDistance</c> — avança pouco, mas o tiro
/// que sai do avanço alcança longe. Não existe um `Kind` "dash + tiro"
/// separado na spec (§3): é este mesmo comportamento, só com dados diferentes.
/// </remarks>
public sealed class DashAttackBehavior : IAbilityBehavior
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
        var frente = AbilityGeometry.FlattenedForward(corpo.GlobalTransform.Basis);
        var origem = corpo.GlobalPosition;

        // MoveAndCollide, e não soma direta em GlobalPosition -- mesmo motivo
        // do MeleeWeapon.Avancar: uma soma direta atravessa parede.
        corpo.MoveAndCollide(frente * ctx.Definition.DashDistance);

        ResolverAcertos(ctx, origem, corpo.GlobalPosition, frente);
    }

    private static void ResolverAcertos(AbilityContext ctx, Vector3 origem, Vector3 destino, Vector3 frente)
    {
        var corpo = ctx.Character.Body;

        // Max, não a distância percorrida sozinha: ver o comentário da classe
        // sobre o Quick Step Shot, cujo Range vai bem além do DashDistance.
        var comprimento = Mathf.Max(origem.DistanceTo(destino), ctx.Definition.Range);

        // Uma vez para o dash inteiro -- mesmo motivo do MeleeArcBehavior.
        var critico = CritMath.RolarNaStats(ctx.Character.Stats);
        var dano = CritMath.AplicarNaStats(
            ctx.Character.Stats,
            ctx.Definition.Damage * (ctx.Character.Stats?.Get(StatId.DamageMultiplier) ?? 1f),
            critico);
        var atingidos = 0;

        AbilityTargeting.ForEachValidTarget(corpo.GetTree(), ctx.TargetGroup, corpo, ctx.Character.Team, alvo =>
        {
            if (!HitscanMath.TryHitSegment(origem, frente, comprimento, alvo.GlobalPosition, ctx.Definition.Radius, out _))
                return true;

            // `!`: AbilityTargeting só chama este callback para alvos com
            // Health vivo -- é a própria checagem que filtra o candidato.
            alvo.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: dano,
                Type: DamageType.Physical,
                HitPoint: alvo.GlobalPosition,
                Direction: frente,
                Knockback: ctx.Definition.Knockback,
                SourceId: corpo.GetInstanceId(),
                SourceTag: ctx.Definition.Id.ToString(),
                IsCritical: critico));

            alvo.Context.Movement?.ApplyKnockback(frente * ctx.Definition.Knockback);
            atingidos++;

            return ctx.Definition.MaxTargets <= 0 || atingidos < ctx.Definition.MaxTargets;
        });
    }
}
