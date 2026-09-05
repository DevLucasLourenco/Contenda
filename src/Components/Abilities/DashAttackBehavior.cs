using Contenda.Characters.Base;
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
        var frenteBruta = -corpo.GlobalTransform.Basis.Z;
        var frente = new Vector3(frenteBruta.X, 0f, frenteBruta.Z).Normalized();

        var origem = corpo.GlobalPosition;

        // MoveAndCollide, e não soma direta em GlobalPosition -- mesmo motivo
        // do MeleeWeapon.Avancar: uma soma direta atravessa parede.
        corpo.MoveAndCollide(frente * ctx.Definition.DashDistance);

        ResolverAcertos(ctx, origem, corpo.GlobalPosition, frente);
    }

    private static void ResolverAcertos(AbilityContext ctx, Vector3 origem, Vector3 destino, Vector3 frente)
    {
        var corpo = ctx.Character.Body;
        var comprimento = origem.DistanceTo(destino);
        var dano = ctx.Definition.Damage * (ctx.Character.Stats?.Get(StatId.DamageMultiplier) ?? 1f);

        // Fronteira com a engine: GetNodesInGroup só aqui, uma vez por
        // execução -- o dash resolve num instante só, não numa janela.
        foreach (var no in corpo.GetTree().GetNodesInGroup(ctx.TargetGroup))
        {
            if (no is not CharacterController alvo || alvo == corpo || !GodotObject.IsInstanceValid(alvo))
                continue;

            if (alvo.Team == ctx.Character.Team)
                continue;

            var vida = alvo.Context?.Health;
            if (vida is null || !vida.IsAlive)
                continue;

            if (!HitscanMath.TryHitSegment(origem, frente, comprimento, alvo.GlobalPosition, ctx.Definition.Radius, out _))
                continue;

            vida.ApplyDamage(new DamageInfo(
                Amount: dano,
                Type: DamageType.Physical,
                HitPoint: alvo.GlobalPosition,
                Direction: frente,
                Knockback: ctx.Definition.Knockback,
                SourceId: corpo.GetInstanceId(),
                SourceTag: ctx.Definition.Id.ToString(),
                IsCritical: false));

            alvo.Context?.Movement?.ApplyKnockback(frente * ctx.Definition.Knockback);
        }
    }
}
