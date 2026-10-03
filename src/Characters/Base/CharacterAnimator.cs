using System.Collections.Generic;
using Contenda.Components.Abilities;
using Contenda.Components.Health;
using Contenda.Components.Transformations;
using Contenda.Weapons;
using Godot;

namespace Contenda.Characters.Base;

/// <summary>Converte eventos do gameplay em apresentação do personagem.</summary>
public sealed partial class CharacterAnimator : Node, ICharacterComponent
{
    private enum AnimationLayer
    {
        Attack,
        Ability,
        Reload,
        Jump,
        Fall,
        Land,
        Dash,
        Dive,
        Hit,
        Transform,
        Death,
    }

    private static readonly StringName LocomotionBlendPositionPath = new("parameters/Locomotion/blend_position");
    private static readonly StringName AttackActivePath = new("parameters/Attack/active");
    private static readonly StringName ReloadActivePath = new("parameters/Reload/active");
    private static readonly (StringName MotionNode, StringName RequestPath)[] OneShotPaths =
    [
        (new("AttackMotion"), new("parameters/Attack/request")),
        (new("AbilityMotion"), new("parameters/Ability/request")),
        (new("ReloadMotion"), new("parameters/Reload/request")),
        (new("JumpMotion"), new("parameters/Jump/request")),
        (new("FallMotion"), new("parameters/Fall/request")),
        (new("LandMotion"), new("parameters/Land/request")),
        (new("DashMotion"), new("parameters/Dash/request")),
        (new("DiveMotion"), new("parameters/Dive/request")),
        (new("HitMotion"), new("parameters/Hit/request")),
        (new("TransformMotion"), new("parameters/Transform/request")),
        (new("DeathMotion"), new("parameters/Death/request")),
    ];

    private CharacterContext? _context;
    private CharacterAnimationSet? _set;
    private AnimationTree? _tree;
    private AnimationNodeBlendTree? _root;
    private AnimationPlayer? _animationPlayer;
    private readonly Dictionary<StringName, StringName> _animationPaths = [];
    private bool _falling;
    private bool _dead;
    private bool _reloadPending;
    private float _deathRemaining;
    private float _attackRemaining;

    public AnimationTree? Tree => _tree;

    public void Bind(CharacterContext context)
    {
        Unsubscribe();
        _context = context;
        if (context.Movement is { } movement)
        {
            movement.JumpStarted += OnJump;
            movement.FallStarted += OnFall;
            movement.Landed += OnLand;
            movement.DashStarted += OnDash;
        }

        if (context.Combat is { } combat)
        {
            combat.AttackStarted += OnAttack;
            combat.DiveStarted += OnDive;
            combat.ReloadStarted += OnReload;
            combat.EquippedWeaponChanged += OnWeaponChanged;
        }

        if (context.Abilities is { } abilities)
            abilities.Executed += OnAbility;
        if (context.Health is { } health)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
        if (context.Transformations is { } transformations)
        {
            transformations.Activated += OnTransform;
            transformations.Reverted += OnRevert;
        }
    }

    public void Configure(CharacterDefinition definition)
    {
        _set = definition.AnimationSet;
        _tree = null;
        _root = null;
        _animationPlayer = null;
        _animationPaths.Clear();
        _dead = false;
        _reloadPending = false;
        _falling = false;
        _deathRemaining = 0f;
        _attackRemaining = 0f;

        if (_context?.Owner.CurrentModel is not { } model || _set is null)
            return;

        CacheAnimationPaths(_set);
        _tree = CharacterPresentation.BuildAnimationTree(model, _set);
        _root = _tree?.TreeRoot as AnimationNodeBlendTree;
        _animationPlayer = model.GetNodeOrNull<AnimationPlayer>(new NodePath("CharacterAnimationPlayer"));
        CharacterPresentation.MountWeapon(model, definition.Weapon, definition.WeaponBoneName);
    }

    public void RefreshWeaponVisual()
    {
        if (_context?.Owner.CurrentModel is not { } model || _context.Owner.Definition is not { } definition)
            return;

        CharacterPresentation.MountWeapon(model, _context.Combat?.EquippedWeapon, definition.WeaponBoneName);
    }

    public void Tick(float delta)
    {
        if (_tree is null || _set is null || _context is null)
            return;

        if (_dead)
        {
            _deathRemaining -= delta;
            if (_deathRemaining <= 0f)
                _tree.Active = false;
            return;
        }

        var velocity = _context.Movement?.Velocity ?? Vector3.Zero;
        var horizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();
        var topSpeed = Mathf.Max(0.01f, _context.Movement?.Settings.MoveSpeed ?? 1f);
        _tree.Set(LocomotionBlendPositionPath, Mathf.Clamp(horizontalSpeed / topSpeed, 0f, 1f));
        _attackRemaining = Mathf.Max(0f, _attackRemaining - delta);

        if (_reloadPending && _attackRemaining <= 0f && !IsReloadActive())
        {
            _reloadPending = false;
            Play(AnimationLayer.Reload, _set.Reload);
        }
    }

    public void ResetForSpawn()
    {
        _dead = false;
        _reloadPending = false;
        _falling = false;
        _deathRemaining = 0f;
        _attackRemaining = 0f;
        if (_tree is not null)
        {
            _tree.Active = true;
            Play(AnimationLayer.Attack, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Ability, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Reload, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Jump, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Fall, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Land, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Dash, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Dive, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Transform, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Death, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Hit, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
        }
    }

    public override void _ExitTree() => Unsubscribe();

    private void Unsubscribe()
    {
        if (_context?.Movement is { } movement)
        {
            movement.JumpStarted -= OnJump;
            movement.FallStarted -= OnFall;
            movement.Landed -= OnLand;
            movement.DashStarted -= OnDash;
        }

        if (_context?.Combat is { } combat)
        {
            combat.AttackStarted -= OnAttack;
            combat.DiveStarted -= OnDive;
            combat.ReloadStarted -= OnReload;
            combat.EquippedWeaponChanged -= OnWeaponChanged;
        }

        if (_context?.Abilities is { } abilities)
            abilities.Executed -= OnAbility;
        if (_context?.Health is { } health)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        if (_context?.Transformations is { } transformations)
        {
            transformations.Activated -= OnTransform;
            transformations.Reverted -= OnRevert;
        }
    }

    private void OnAttack(int step)
    {
        if (_context?.Combat is not { } combat || _set is null)
            return;

        if (combat.EquippedWeapon.Kind == WeaponKind.Melee)
        {
            var attacks = _context.Movement is { IsGroundedConfiavel: false }
                ? _set.AerialAttacks
                : _set.MeleeAttacks;
            if (attacks.Length == 0)
                return;
            var clip = attacks[Mathf.Clamp(step - 1, 0, attacks.Length - 1)];
            Play(AnimationLayer.Attack, clip);
            _attackRemaining = AnimationLength(clip);
            return;
        }

        var isCannon = _context.Transformations?.Active?.ShowCannonVisual == true;
        var rangedClip = isCannon ? _set.ArmCannonShoot : _set.Shoot;
        Play(AnimationLayer.Attack, rangedClip);
        _attackRemaining = AnimationLength(rangedClip);
    }

    private void OnAbility(AbilityDefinition ability)
    {
        if (_set is null)
            return;

        var clip = _set.AnimationForAbility(ability.Id);
        if (!clip.IsEmpty)
            Play(AnimationLayer.Ability, clip);
    }

    private void OnReload()
    {
        if (_attackRemaining > 0f || IsAttackActive())
        {
            _reloadPending = true;
            return;
        }

        if (_set is not null)
            Play(AnimationLayer.Reload, _set.Reload);
    }

    private void OnJump()
    {
        if (_set is not null)
        {
            Play(AnimationLayer.Fall, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Land, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Jump, _set.Jump);
        }
        _falling = false;
    }

    private void OnFall() { if (_set is not null) Play(AnimationLayer.Fall, _set.Fall); _falling = true; }
    private void OnLand()
    {
        if (_set is not null && _falling)
        {
            Play(AnimationLayer.Fall, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Land, _set.Land);
        }
        _falling = false;
    }
    private void OnDash() { if (_set is not null) Play(AnimationLayer.Dash, _set.Dash); }
    private void OnDive() { if (_set is not null) Play(AnimationLayer.Dive, _set.Dive); }
    private void OnDamaged(DamageInfo _) { if (!_dead && _set is not null) Play(AnimationLayer.Hit, _set.Hit); }
    private void OnTransform(TransformationDefinition _) { if (_set is not null) Play(AnimationLayer.Transform, _set.Transform); }
    private void OnRevert(TransformationDefinition _, RevertReason __) { if (_set is not null) Play(AnimationLayer.Transform, _set.Transform); }
    private void OnWeaponChanged(WeaponDefinition _) => RefreshWeaponVisual();

    private void OnDied(DamageInfo _)
    {
        _dead = true;
        _reloadPending = false;
        if (_set is null)
            return;

        Play(AnimationLayer.Death, _set.Death);
        _deathRemaining = AnimationLength(_set.Death) + 0.12f;
    }

    private void Play(AnimationLayer layer, StringName clip, AnimationNodeOneShot.OneShotRequest request = AnimationNodeOneShot.OneShotRequest.Fire)
    {
        if (_tree is null || _root is null)
            return;

        var paths = OneShotPaths[(int)layer];
        if (!clip.IsEmpty
            && _animationPaths.TryGetValue(clip, out var animationPath)
            && _root.GetNode(paths.MotionNode) is AnimationNodeAnimation animationNode)
        {
            animationNode.Animation = animationPath;
        }

        _tree.Set(paths.RequestPath, (int)request);
    }

    private bool IsAttackActive() => _tree?.Get(AttackActivePath).AsBool() == true;

    private bool IsReloadActive() => _tree?.Get(ReloadActivePath).AsBool() == true;

    private void CacheAnimationPaths(CharacterAnimationSet set)
    {
        Cache(set.Idle);
        Cache(set.Walk);
        Cache(set.Run);
        Cache(set.Shoot);
        Cache(set.Reload);
        Cache(set.ArmCannonShoot);
        Cache(set.Jump);
        Cache(set.Fall);
        Cache(set.Land);
        Cache(set.Dash);
        Cache(set.Dive);
        Cache(set.Hit);
        Cache(set.Death);
        Cache(set.Transform);

        foreach (var clip in set.MeleeAttacks)
            Cache(clip);
        foreach (var clip in set.AerialAttacks)
            Cache(clip);
        foreach (var binding in set.AbilityAnimations)
            Cache(binding.Animation);

        void Cache(StringName clip)
        {
            if (!clip.IsEmpty && !_animationPaths.ContainsKey(clip))
                _animationPaths.Add(clip, new StringName($"motion/{clip}"));
        }
    }

    private float AnimationLength(StringName clip)
    {
        if (_animationPlayer is null || !_animationPaths.TryGetValue(clip, out var animationPath))
            return 0.8f;

        return (float)(_animationPlayer.GetAnimation(animationPath)?.Length ?? 0.8);
    }
}
