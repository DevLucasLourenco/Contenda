using Contenda.Components.Health;
using Contenda.Components.Stats;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>Dano num cone estreito à frente, e lança o alvo para cima.</summary>
/// <remarks>
/// Rising Slash — spec 05 §5. Mesma varredura do <see cref="MeleeArcBehavior"/>
/// (cone + alcance, sem deslocamento do personagem); a única diferença real é
/// a direção da repulsão, sempre <see cref="Vector3.Up"/> em vez de "para
/// longe do golpe" — é o que faz o alvo subir em vez de escorregar para trás.
/// O combate aéreo de verdade (perseguir no ar, prolongar o juggle) é o
/// ticket 19; aqui só o impulso inicial precisa existir.
/// </remarks>
public sealed class UppercutBehavior : IAbilityBehavior
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
        var frente = AbilityGeometry.FlattenedForward(corpo.GlobalTransform.Basis);

        var alcanceQuadrado = ctx.Definition.Range * ctx.Definition.Range;
        var cosseno = Mathf.Cos(Mathf.DegToRad(ctx.Definition.Angle));

        // Uma vez para o golpe inteiro -- mesmo motivo do MeleeArcBehavior.
        var critico = CritMath.RolarNaStats(ctx.Character.Stats);
        var dano = CritMath.AplicarNaStats(
            ctx.Character.Stats,
            ctx.Definition.Damage * (ctx.Character.Stats?.Get(StatId.DamageMultiplier) ?? 1f),
            critico);
        var lancamento = Vector3.Up * ctx.Definition.Knockback;
        var atingidos = 0;

        AbilityTargeting.ForEachValidTarget(corpo.GetTree(), ctx.TargetGroup, corpo, ctx.Character.Team, alvo =>
        {
            var ate = alvo.GlobalPosition - origem;
            var noPlano = new Vector3(ate.X, 0f, ate.Z);
            if (noPlano.LengthSquared() > alcanceQuadrado)
                return true;

            var direcao = noPlano.Length() > 0.001f ? noPlano.Normalized() : frente;
            if (frente.Dot(direcao) < cosseno)
                return true;

            // `!`: AbilityTargeting só chama este callback para alvos com
            // Health vivo -- é a própria checagem que filtra o candidato.
            alvo.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: dano,
                Type: DamageType.Physical,
                HitPoint: alvo.GlobalPosition,
                Direction: Vector3.Up,
                Knockback: ctx.Definition.Knockback,
                SourceId: corpo.GetInstanceId(),
                SourceTag: ctx.Definition.Id.ToString(),
                IsCritical: critico));

            alvo.Context.Movement?.ApplyKnockback(lancamento);
            atingidos++;

            return ctx.Definition.MaxTargets <= 0 || atingidos < ctx.Definition.MaxTargets;
        });
    }
}
