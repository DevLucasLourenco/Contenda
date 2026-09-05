using Contenda.Components.Health;
using Contenda.Components.Stats;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>Dano num cone (ou círculo inteiro) ao redor do personagem, sem deslocamento.</summary>
/// <remarks>
/// Spin Slash — spec 05 §5. <see cref="AbilityDefinition.Angle"/> é a
/// meia-abertura do cone, igual ao <c>MeleeWeapon</c>: 180° faz o cosseno de
/// referência valer -1, e nenhum alvo dentro do alcance fica de fora — é assim
/// que Spin Slash vira um giro de 360° sem um caso especial "círculo inteiro"
/// no código, só um número no `.tres`.
/// </remarks>
public sealed class MeleeArcBehavior : IAbilityBehavior
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
        var dano = ctx.Definition.Damage * (ctx.Character.Stats?.Get(StatId.DamageMultiplier) ?? 1f);
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
