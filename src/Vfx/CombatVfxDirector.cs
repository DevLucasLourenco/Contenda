using System.Collections.Generic;
using Contenda.Components.Abilities;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.Weapons;
using Godot;

namespace Contenda.Vfx;

internal enum CombatVfxCue : byte
{
    MeleeSlash,
    RevolverMuzzle,
    CannonMuzzle,
    Tracer,
    NormalImpact,
    CriticalImpact,
    ExplosionImpact,
    Transformation,
    EnemySpawn,
    EnemyDeath,
    AbilityDashSlash,
    AbilityDeadeye,
    AbilitySpinSlash,
    AbilityRisingSlash,
    AbilityQuickStepShot,
    AbilityHeavyLunge,
    AbilityFanTheHammer,
    AbilityExplosiveShot,
}

/// <summary>Entrega efeitos de combate a pools mesh pré-alocados.</summary>
public sealed partial class CombatVfxDirector : Node
{
    private static readonly EffectSpec[] EffectSpecs =
    [
        new(CombatVfxCue.MeleeSlash, CombatVfxPattern.Sweep, new Color(0.2f, 0.85f, 1f), 0.16f, 8, 1f),
        new(CombatVfxCue.RevolverMuzzle, CombatVfxPattern.Flash, new Color(1f, 0.64f, 0.18f), 0.08f, 3, 0.7f),
        new(CombatVfxCue.CannonMuzzle, CombatVfxPattern.Flash, new Color(0.22f, 0.85f, 1f), 0.13f, 3, 1.2f),
        new(CombatVfxCue.Tracer, CombatVfxPattern.Tracer, new Color(1f, 0.9f, 0.52f), 0.075f, 5, 1f),
        new(CombatVfxCue.NormalImpact, CombatVfxPattern.Burst, new Color(0.85f, 0.92f, 1f), 0.22f, 10, 0.7f),
        new(CombatVfxCue.CriticalImpact, CombatVfxPattern.Explosion, new Color(1f, 0.83f, 0.12f), 0.36f, 8, 1.15f),
        new(CombatVfxCue.ExplosionImpact, CombatVfxPattern.Explosion, new Color(1f, 0.25f, 0.06f), 0.42f, 8, 1.45f),
        new(CombatVfxCue.Transformation, CombatVfxPattern.Ring, new Color(0.35f, 0.95f, 0.75f), 0.55f, 3, 1.7f),
        new(CombatVfxCue.EnemySpawn, CombatVfxPattern.Ring, new Color(0.45f, 0.9f, 0.35f), 0.42f, 8, 1.2f),
        new(CombatVfxCue.EnemyDeath, CombatVfxPattern.Death, new Color(0.78f, 0.42f, 1f), 0.4f, 10, 1f),
        new(CombatVfxCue.AbilityDashSlash, CombatVfxPattern.Sweep, new Color(0.12f, 0.9f, 1f), 0.24f, 3, 1.35f),
        new(CombatVfxCue.AbilityDeadeye, CombatVfxPattern.Needle, new Color(1f, 0.91f, 0.3f), 0.2f, 3, 1.35f),
        new(CombatVfxCue.AbilitySpinSlash, CombatVfxPattern.Ring, new Color(0.85f, 0.45f, 1f), 0.35f, 3, 1.5f),
        new(CombatVfxCue.AbilityRisingSlash, CombatVfxPattern.Cross, new Color(0.25f, 1f, 0.55f), 0.28f, 3, 1.1f),
        new(CombatVfxCue.AbilityQuickStepShot, CombatVfxPattern.Flash, new Color(0.3f, 0.72f, 1f), 0.18f, 3, 1.35f),
        new(CombatVfxCue.AbilityHeavyLunge, CombatVfxPattern.Arc, new Color(1f, 0.42f, 0.16f), 0.32f, 3, 1.6f),
        new(CombatVfxCue.AbilityFanTheHammer, CombatVfxPattern.Burst, new Color(1f, 0.28f, 0.47f), 0.34f, 3, 1.3f),
        new(CombatVfxCue.AbilityExplosiveShot, CombatVfxPattern.Explosion, new Color(1f, 0.2f, 0.04f), 0.42f, 3, 1.6f),
    ];

    private readonly Dictionary<CombatVfxCue, EffectPool> _pools = [];

    public override void _Ready()
    {
        ServiceLocator.Register(this);
        foreach (var spec in EffectSpecs)
            _pools.Add(spec.Cue, new EffectPool(this, spec));

        ServiceLocator.Events.MeleeSwingRequested += AoGolpeCorpoACorpo;
        ServiceLocator.Events.ShotFired += AoDisparar;
        ServiceLocator.Events.DamageImpactRequested += AoAcertar;
        ServiceLocator.Events.AbilityCastRequested += AoUsarHabilidade;
        ServiceLocator.Events.TransformationChanged += AoTransformar;
        ServiceLocator.Events.EnemySpawned += AoNascerInimigo;
        ServiceLocator.Events.EnemyDied += AoMorrerInimigo;
        GameLog.Debug("[boot] CombatVfxDirector pronto (pools pré-alocados)");
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.MeleeSwingRequested -= AoGolpeCorpoACorpo;
        ServiceLocator.Events.ShotFired -= AoDisparar;
        ServiceLocator.Events.DamageImpactRequested -= AoAcertar;
        ServiceLocator.Events.AbilityCastRequested -= AoUsarHabilidade;
        ServiceLocator.Events.TransformationChanged -= AoTransformar;
        ServiceLocator.Events.EnemySpawned -= AoNascerInimigo;
        ServiceLocator.Events.EnemyDied -= AoMorrerInimigo;
    }

    private void AoGolpeCorpoACorpo(MeleeSwingEvent evento)
        => Tocar(CombatVfxCue.MeleeSlash, evento.Position + Vector3.Up, evento.Direction, 1f);

    private void AoDisparar(ShotFiredEvent evento)
    {
        var isCannon = evento.WeaponId == WeaponDefinition.ArmCannonId;
        var direction = evento.Destination - evento.Origin;
        var muzzle = isCannon ? CombatVfxCue.CannonMuzzle : CombatVfxCue.RevolverMuzzle;
        Tocar(muzzle, evento.Origin, direction, 1f);

        var length = direction.Length();
        if (length > 0.01f)
            Tocar(CombatVfxCue.Tracer, evento.Origin + (direction * 0.5f), direction, 1f, length);
    }

    private void AoAcertar(DamageImpactEvent evento)
    {
        var cue = evento.IsCritical
            ? CombatVfxCue.CriticalImpact
            : evento.Type == DamageType.Explosive ? CombatVfxCue.ExplosionImpact : CombatVfxCue.NormalImpact;
        Tocar(cue, evento.Position + (Vector3.Up * 0.7f), Vector3.Up, 1f);
    }

    private void AoUsarHabilidade(AbilityCastPresentationEvent evento)
    {
        var cue = evento.Style switch
        {
            AbilityVfxStyle.DashSlash => CombatVfxCue.AbilityDashSlash,
            AbilityVfxStyle.Deadeye => CombatVfxCue.AbilityDeadeye,
            AbilityVfxStyle.SpinSlash => CombatVfxCue.AbilitySpinSlash,
            AbilityVfxStyle.RisingSlash => CombatVfxCue.AbilityRisingSlash,
            AbilityVfxStyle.QuickStepShot => CombatVfxCue.AbilityQuickStepShot,
            AbilityVfxStyle.HeavyLunge => CombatVfxCue.AbilityHeavyLunge,
            AbilityVfxStyle.FanTheHammer => CombatVfxCue.AbilityFanTheHammer,
            AbilityVfxStyle.ExplosiveShot => CombatVfxCue.AbilityExplosiveShot,
            _ => (CombatVfxCue?)null,
        };

        if (cue is { } value)
            Tocar(value, evento.Position + Vector3.Up, evento.Direction, 1f, tint: evento.Tint);
    }

    private void AoTransformar(TransformationPresentationEvent evento)
    {
        if (evento.Activated)
            Tocar(CombatVfxCue.Transformation, evento.Position + Vector3.Up, Vector3.Up, 1.3f, tint: evento.Tint);
    }

    private void AoNascerInimigo(EnemySpawnedPresentationEvent evento)
        => Tocar(CombatVfxCue.EnemySpawn, evento.Position, Vector3.Up, evento.IsBoss ? 1.7f : 1f);

    private void AoMorrerInimigo(EnemyDiedPresentationEvent evento)
        => Tocar(CombatVfxCue.EnemyDeath, evento.Position + Vector3.Up, Vector3.Up, evento.IsBoss ? 1.7f : 1f);

    private void Tocar(CombatVfxCue cue, Vector3 position, Vector3 direction, float size, float length = 1f, Color? tint = null)
    {
        if (_pools.TryGetValue(cue, out var pool))
            pool.Play(position, direction, size, length, tint);
    }

    private sealed record EffectSpec(CombatVfxCue Cue, CombatVfxPattern Pattern, Color Tint, float Duration, int Capacity, float Size);

    private sealed class EffectPool
    {
        private readonly List<CombatVfxEffect> _effects;
        private readonly EffectSpec _spec;

        public EffectPool(Node owner, EffectSpec spec)
        {
            _spec = spec;
            _effects = new List<CombatVfxEffect>(spec.Capacity);
            for (var i = 0; i < spec.Capacity; i++)
            {
                var effect = new CombatVfxEffect();
                effect.Initialize(spec.Pattern, spec.Duration);
                owner.AddChild(effect);
                _effects.Add(effect);
            }
        }

        public void Play(Vector3 position, Vector3 direction, float size, float length, Color? tint)
        {
            foreach (var effect in _effects)
            {
                if (!effect.IsAvailable)
                    continue;

                effect.Play(position, direction, _spec.Size * size, tint ?? _spec.Tint, length);
                return;
            }
        }
    }
}
