using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Components.Stats;
using Contenda.Weapons;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>N raycasts em sequência, cada um numa direção aleatória dentro de um cone.</summary>
/// <remarks>
/// Fan The Hammer — spec 05 §5, "6 × 12 dano, 10 m, cone 30°". Reaproveita
/// <see cref="AbilityDefinition.MaxTargets"/> para o NÚMERO DE TIROS aqui, não
/// "quantos alvos" — o mesmo campo já muda de sentido por <c>Kind</c> em
/// <see cref="AbilityDefinition.Range"/>/<see cref="AbilityDefinition.Radius"/>
/// /<see cref="AbilityDefinition.Angle"/>, e um campo novo só para isto
/// duplicaria o que já existe. Cada tiro para no alvo mais próximo no próprio
/// caminho, como o <c>HitscanWeapon</c> do revólver — ao contrário do
/// <see cref="HitscanShotBehavior"/>, esta não é perfurante.
/// </remarks>
public sealed class HitscanBurstBehavior : IAbilityBehavior
{
    /// <summary>Quantos tiros, se o `.tres` não configurar <c>MaxTargets</c>.</summary>
    private const int TirosPadrao = 6;

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
        var direcaoBase = AbilityGeometry.AimOrFlattenedForward(ctx.AimDirection, corpo.GlobalTransform.Basis);

        var dano = ctx.Definition.Damage * (ctx.Character.Stats?.Get(StatId.DamageMultiplier) ?? 1f);
        var tiros = ctx.Definition.MaxTargets > 0 ? ctx.Definition.MaxTargets : TirosPadrao;

        for (var i = 0; i < tiros; i++)
        {
            var anguloGraus = (float)GD.RandRange(-ctx.Definition.Angle, ctx.Definition.Angle);
            var direcao = direcaoBase.Rotated(Vector3.Up, Mathf.DegToRad(anguloGraus));

            DispararUm(ctx, origem, direcao, dano);
        }
    }

    private static void DispararUm(AbilityContext ctx, Vector3 origem, Vector3 direcao, float dano)
    {
        var corpo = ctx.Character.Body;
        CharacterController? melhor = null;
        var melhorDistancia = float.MaxValue;

        AbilityTargeting.ForEachValidTarget(corpo.GetTree(), ctx.TargetGroup, corpo, ctx.Character.Team, alvo =>
        {
            if (!HitscanMath.TryHitSegment(origem, direcao, ctx.Definition.Range, alvo.GlobalPosition, ctx.Definition.Radius, out var distancia))
                return true;

            if (distancia < melhorDistancia)
            {
                melhorDistancia = distancia;
                melhor = alvo;
            }

            return true;
        });

        if (melhor is null)
            return;

        // Cada tiro do leque é sorteado por si: são seis disparos em
        // sequência, não um golpe em área simultâneo como o Spin Slash --
        // mais parecido com apertar o gatilho do revólver seis vezes
        // seguidas do que com um cone que acerta cinco alvos de uma vez.
        // Ticket 18, spec 16 §5.
        var critico = CritMath.RolarNaStats(ctx.Character.Stats);
        var danoFinal = CritMath.AplicarNaStats(ctx.Character.Stats, dano, critico);

        // `!`: AbilityTargeting só chama o callback acima para alvos com
        // Health vivo -- é a própria checagem que filtra quem vira `melhor`.
        melhor.Context!.Health!.ApplyDamage(new DamageInfo(
            Amount: danoFinal,
            Type: DamageType.Physical,
            HitPoint: melhor.GlobalPosition,
            Direction: direcao,
            Knockback: ctx.Definition.Knockback,
            SourceId: corpo.GetInstanceId(),
            SourceTag: ctx.Definition.Id.ToString(),
            IsCritical: critico));

        melhor.Context.Movement?.ApplyKnockback(direcao * ctx.Definition.Knockback);
    }
}
