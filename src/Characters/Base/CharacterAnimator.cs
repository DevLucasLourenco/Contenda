using System.Collections.Generic;
using Contenda.Components.Abilities;
using Contenda.Components.AI;
using Contenda.Components.Health;
using Contenda.Components.Transformations;
using Contenda.Core;
using Contenda.Weapons;
using Godot;

namespace Contenda.Characters.Base;

/// <summary>Converte eventos do gameplay em apresentação do personagem.</summary>
public sealed partial class CharacterAnimator : Node, ICharacterComponent
{
    private enum AnimationLayer
    {
        Attack,
        Alert,
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
        (new("AlertMotion"), new("parameters/Alert/request")),
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
    private EnemyBrain? _enemyBrain;
    private CharacterAnimationSet? _animationSet;
    private AnimationTree? _tree;
    private AnimationNodeBlendTree? _root;
    private AnimationPlayer? _animationPlayer;
    private BoneAttachment3D? _weaponSocket;
    private readonly Dictionary<StringName, StringName> _animationPaths = [];
    private bool _falling;
    private bool _diving;
    private bool _dead;
    private bool _playerAimActive;
    private bool _reloadPending;
    private float _deathRemaining;
    private float _attackRemaining;

    public AnimationTree? Tree => _tree;
    public AnimationPlayer? AnimationPlayer => _animationPlayer;
    public BoneAttachment3D? WeaponSocket => _weaponSocket;

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
        if (_enemyBrain is not null)
            _enemyBrain.StateChanged -= OnEnemyStateChanged;
        _enemyBrain = _context?.EnemyBrain;
        if (_enemyBrain is not null)
            _enemyBrain.StateChanged += OnEnemyStateChanged;

        var previousSocket = _weaponSocket;
        _animationSet = definition.AnimationSet;
        _tree = null;
        _root = null;
        _animationPlayer = null;
        _animationPaths.Clear();
        _dead = false;
        _playerAimActive = false;
        _reloadPending = false;
        _falling = false;
        _diving = false;
        _deathRemaining = 0f;
        _attackRemaining = 0f;

        if (_context?.Owner.CurrentModel is not { } model || _animationSet is null)
        {
            if (previousSocket is not null
                && GodotObject.IsInstanceValid(previousSocket)
                && previousSocket.GetParent() is { } parent)
            {
                parent.RemoveChild(previousSocket);
                previousSocket.Free();
            }
            _weaponSocket = null;
            return;
        }

        CacheAnimationPaths(_animationSet);
        var presentation = CharacterPresentation.BuildAnimationTree(model, _animationSet);
        _tree = presentation?.Tree;
        _root = _tree?.TreeRoot as AnimationNodeBlendTree;
        _animationPlayer = presentation?.Player;
        _weaponSocket = CharacterPresentation.MountWeapon(
            _context.Owner.CurrentSkeleton, definition.Weapon, definition.WeaponBoneName, previousSocket);

        if (_context.Team == Team.Player
            && _context.Combat?.EquippedWeapon.Kind == WeaponKind.Hitscan
            && !_animationSet.Alert.IsEmpty
            && _root?.GetNode("Alert") is AnimationNodeOneShot aimPose)
        {
            aimPose.Autorestart = true;
            aimPose.AutorestartDelay = 0f;
        }
    }

    public void RefreshWeaponVisual()
    {
        if (_context?.Owner.CurrentModel is not { } model || _context.Owner.Definition is not { } definition)
            return;

        _weaponSocket = CharacterPresentation.MountWeapon(
            _context.Owner.CurrentSkeleton, _context.Combat?.EquippedWeapon, definition.WeaponBoneName, _weaponSocket);
    }

    public void Tick(float delta)
    {
        if (_tree is null || _animationSet is null || _context is null)
            return;

        if (_dead)
        {
            _deathRemaining -= delta;
            if (_deathRemaining <= 0f)
                _tree.Active = false;
            return;
        }

        AtualizarMiraDoJogador();

        var velocity = _context.Movement?.Velocity ?? Vector3.Zero;
        var horizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();
        var topSpeed = Mathf.Max(0.01f, _context.Movement?.Settings.MoveSpeed ?? 1f);
        _tree.Set(LocomotionBlendPositionPath, Mathf.Clamp(horizontalSpeed / topSpeed, 0f, 1f));
        _attackRemaining = Mathf.Max(0f, _attackRemaining - delta);

        if (_reloadPending && _attackRemaining <= 0f && !IsAttackActive() && !IsReloadActive())
        {
            _reloadPending = false;
            Play(AnimationLayer.Reload, _animationSet.Reload);
        }
    }

    public void ResetForSpawn()
    {
        _dead = false;
        _playerAimActive = false;
        _reloadPending = false;
        _falling = false;
        _diving = false;
        _deathRemaining = 0f;
        _attackRemaining = 0f;
        if (_tree is not null)
        {
            _tree.Active = true;
            Play(AnimationLayer.Attack, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Alert, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Ability, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Reload, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Jump, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Fall, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Land, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Dash, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Dive, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
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

        if (_enemyBrain is not null)
        {
            _enemyBrain.StateChanged -= OnEnemyStateChanged;
            _enemyBrain = null;
        }
    }

    private void OnAttack(int step)
    {
        if (_context?.Combat is not { } combat || _animationSet is null)
            return;

        if (combat.EquippedWeapon.Kind == WeaponKind.Melee)
        {
            var attacks = _context.Movement is { IsGroundedConfiavel: false }
                ? _animationSet.AerialAttacks
                : _animationSet.MeleeAttacks;
            if (attacks.Length == 0)
                return;
            var clip = attacks[Mathf.Clamp(step - 1, 0, attacks.Length - 1)];
            Play(AnimationLayer.Attack, clip);
            _attackRemaining = AnimationLength(clip);
            return;
        }

        var isCannon = _context.Transformations?.Active?.ShowCannonVisual == true;
        var rangedClip = isCannon ? _animationSet.ArmCannonShoot : _animationSet.Shoot;
        Play(AnimationLayer.Attack, rangedClip);
        _attackRemaining = AnimationLength(rangedClip);
    }

    private void OnEnemyStateChanged(EnemyState _, EnemyState current)
    {
        if (!_dead && current == EnemyState.Alert && _animationSet is { Alert.IsEmpty: false } set)
            Play(AnimationLayer.Alert, set.Alert);
    }

    private void OnAbility(AbilityDefinition ability)
    {
        if (_animationSet is null)
            return;

        var clip = _animationSet.AnimationForAbility(ability.Id);
        if (!clip.IsEmpty)
            Play(AnimationLayer.Ability, clip);
    }

    private void OnReload()
    {
        _reloadPending = _animationSet is not null;
    }

    private void OnJump()
    {
        if (_animationSet is not null)
        {
            Play(AnimationLayer.Fall, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Land, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Dive, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Jump, _animationSet.Jump);
        }
        _falling = false;
        _diving = false;
    }

    private void OnFall() { if (_animationSet is not null) Play(AnimationLayer.Fall, _animationSet.Fall); _falling = true; }
    private void OnLand()
    {
        if (_diving)
            Play(AnimationLayer.Dive, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);

        if (_animationSet is not null && _falling)
        {
            Play(AnimationLayer.Fall, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
            Play(AnimationLayer.Land, _animationSet.Land);
        }

        _falling = false;
        _diving = false;
    }
    private void OnDash() { if (_animationSet is not null) Play(AnimationLayer.Dash, _animationSet.Dash); }
    private void OnDive()
    {
        if (_animationSet is null)
            return;

        _diving = true;
        Play(AnimationLayer.Dive, _animationSet.Dive);
    }
    private void OnDamaged(DamageInfo _) { if (!_dead && _animationSet is not null) Play(AnimationLayer.Hit, _animationSet.Hit); }
    private void OnTransform(TransformationDefinition _) { if (_animationSet is not null) Play(AnimationLayer.Transform, _animationSet.Transform); }
    private void OnRevert(TransformationDefinition _, RevertReason __) { if (_animationSet is not null) Play(AnimationLayer.Transform, _animationSet.Transform); }
    private void OnWeaponChanged(WeaponDefinition _) => RefreshWeaponVisual();

    private void AtualizarMiraDoJogador()
    {
        var mirando = _context?.Team == Team.Player
            && _context.Combat?.EquippedWeapon.Kind == WeaponKind.Hitscan
            && _context.Targeting?.HasAim == true
            && _animationSet is { Alert.IsEmpty: false };

        if (_playerAimActive == mirando)
            return;

        _playerAimActive = mirando;
        Play(
            AnimationLayer.Alert,
            mirando ? _animationSet!.Alert : new StringName(),
            mirando ? AnimationNodeOneShot.OneShotRequest.Fire : AnimationNodeOneShot.OneShotRequest.Abort);
    }

    private void OnDied(DamageInfo _)
    {
        _dead = true;
        _playerAimActive = false;
        Play(AnimationLayer.Alert, new StringName(), AnimationNodeOneShot.OneShotRequest.Abort);
        _reloadPending = false;
        _diving = false;
        if (_animationSet is null)
            return;

        Play(AnimationLayer.Death, _animationSet.Death);
        _deathRemaining = AnimationLength(_animationSet.Death) + 0.12f;
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
        foreach (var clip in set.EnumerateReferencedClips())
            Cache(clip);

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
