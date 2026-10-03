using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Combat;
using Contenda.Components.Stats;
using Contenda.Core;
using Godot;

namespace Contenda.Components.Transformations;

/// <summary>Seleciona, ativa e reverte as formas definidas para o personagem.</summary>
public sealed partial class TransformationComponent : Node, ICharacterComponent
{
    [Export] public NodePath BodyMeshPath { get; set; } = new("%Corpo");
    [Export] public NodePath CannonVisualPath { get; set; } = new("%CannonForma");
    [Export] public TransformationDefinition[] Forms { get; set; } = [];

    private CharacterContext? _context;
    private TransformationState _state = new(0);
    private FormRuntime[] _formRuntimes = [];
    private FormRuntime? _activeRuntime;
    private MeshInstance3D? _bodyMesh;
    private Node3D? _cannonVisual;
    private Material? _originalOverride;
    private StandardMaterial3D? _formMaterial;
    private MeshInstance3D? _auraRing;
    private StandardMaterial3D? _auraMaterial;
    private float _auraElapsed;

    public IReadOnlyList<TransformationDefinition> Available => Forms;
    public int SelectedIndex => _state.SelectedIndex;
    public TransformationDefinition? Active => _state.IsActive ? Forms[_state.ActiveIndex - 1] : null;
    public float ActiveDuration => _state.ActiveDuration;
    public bool CannonVisible => Active?.ShowCannonVisual == true
        && _context?.Combat?.EquippedWeapon.ModelScene is not null;

    public event Action<int>? SelectionChanged;
    public event Action<TransformationDefinition>? Activated;
    public event Action<TransformationDefinition, RevertReason>? Reverted;

    public override void _Ready()
    {
        _bodyMesh = GetNodeOrNull<MeshInstance3D>(BodyMeshPath);
        _cannonVisual = GetNodeOrNull<Node3D>(CannonVisualPath);
        if (_bodyMesh is null)
            throw new InvalidOperationException("TransformationComponent: Corpo precisa existir na cena.");

        _originalOverride = _bodyMesh.MaterialOverride;
        _formMaterial = new StandardMaterial3D { Roughness = 0.6f };
        SetProcess(false);
    }

    public void Bind(CharacterContext contexto)
    {
        if (_context?.Health is not null)
            _context.Health.BeforeDied -= AoMorrer;

        _context = contexto;
        GarantirAura(contexto.Body);
        _context.BodyBaseMaterial = _originalOverride;
        if (_context.Health is not null)
            _context.Health.BeforeDied += AoMorrer;
    }

    /// <summary>Aplica forma e flash à malha real do arquétipo montado na cena.</summary>
    public void UseBodyMesh(MeshInstance3D body)
    {
        _bodyMesh = body;
        _originalOverride = body.MaterialOverride;
        if (_context is not null)
            _context.BodyBaseMaterial = _originalOverride;
    }

    public void Configure(CharacterDefinition definition)
    {
        if (_state.IsActive)
            Revert(RevertReason.ModeReset);

        Forms = definition.Transformations ?? [];
        _state = new TransformationState(Forms.Length);
        _activeRuntime = null;
        _formRuntimes = new FormRuntime[Forms.Length];
        for (var i = 0; i < Forms.Length; i++)
        {
            var forma = Forms[i];
            var source = $"form:{forma.Id}";
            _formRuntimes[i] = new FormRuntime(source,
            [
                new StatModifier(StatId.DamageMultiplier, ModifierOp.PercentMult, forma.DamageMultiplier - 1f, source),
                new StatModifier(StatId.MoveSpeed, ModifierOp.PercentMult, forma.SpeedMultiplier - 1f, source),
                new StatModifier(StatId.DefenseMultiplier, ModifierOp.PercentMult, forma.DefenseMultiplier - 1f, source),
                new StatModifier(StatId.CritChance, ModifierOp.Flat, forma.CritChanceBonus, source),
            ]);
        }

        if (_cannonVisual is not null)
            _cannonVisual.Visible = false;
        if (_auraRing is not null)
            _auraRing.Visible = false;
        SelectionChanged?.Invoke(0);
    }

    public void SelectNext()
    {
        if (((_context?.Combat?.ActiveLocks ?? ActionLock.None) & ActionLock.Forms) != 0)
            return;

        SelectionChanged?.Invoke(_state.SelectNext());
    }

    public void SelectPrevious()
    {
        if (((_context?.Combat?.ActiveLocks ?? ActionLock.None) & ActionLock.Forms) != 0)
            return;

        SelectionChanged?.Invoke(_state.SelectPrevious());
    }

    public bool TryActivateSelected()
    {
        if (_context is null || ((_context.Combat?.ActiveLocks ?? ActionLock.None) & ActionLock.Forms) != 0)
            return false;

        if (_state.IsActive)
        {
            var formaAtiva = Active;
            if (formaAtiva is not null && _state.CanRevert(formaAtiva.MinimumDuration))
                Revert(RevertReason.Manual);
            return false;
        }

        var indice = _state.SelectedIndex;
        if (indice == 0 || indice > Forms.Length)
            return false;

        var forma = Forms[indice - 1];
        if (_context.Mana is null || !_context.Mana.TryConsume(forma.ManaActivationCost))
            return false;

        if (!_state.TryActivate(forma.ManaActivationCost, forma.ManaActivationCost))
            return false;

        _activeRuntime = _formRuntimes[indice - 1];
        Aplicar(forma, _activeRuntime);
        Activated?.Invoke(forma);
        return true;
    }

    public void Tick(float delta, in Contenda.Input.IntentFrame intent)
    {
        if (intent.FormScrollDelta > 0) SelectNext();
        else if (intent.FormScrollDelta < 0) SelectPrevious();

        if (intent.FormActivatePressed)
            TryActivateSelected();

        if (Active is not { } forma || _context?.Mana is null)
            return;

        _state.Advance(delta);
        _context.Mana.Drain(forma.ManaDrainPerSecond * delta);
        if (_context.Mana.Current <= 0f)
            Revert(RevertReason.ManaDepleted);
    }

    public void Revert(RevertReason reason)
    {
        var forma = Active;
        if (forma is null || _context is null || _activeRuntime is null)
            return;

        _context.Stats?.RemoveBySource(_activeRuntime.Source);
        _context.Movement?.SetExtraAirJumps(0);
        _context.Combat?.SetWeaponOverride(null);
        _context.BodyBaseMaterial = _originalOverride;
        if (_bodyMesh is not null)
            _bodyMesh.MaterialOverride = _originalOverride;
        if (_cannonVisual is not null)
            _cannonVisual.Visible = false;

        _state.Revert();
        _activeRuntime = null;
        if (_auraRing is not null)
            _auraRing.Visible = false;
        ServiceLocator.Events.RaiseTransformationChanged(new TransformationPresentationEvent(
            forma.Id,
            _context.Body.GlobalPosition,
            forma.ThemeColor,
            Activated: false));
        Reverted?.Invoke(forma, reason);
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (_auraRing is null || !_auraRing.Visible)
            return;

        _auraElapsed += (float)delta;
        var pulse = 1f + (Mathf.Sin(_auraElapsed * 4.5f) * 0.12f);
        _auraRing.Scale = new Vector3(pulse, pulse, pulse);
    }

    public void ResetForSpawn()
    {
        Revert(RevertReason.ModeReset);
        _state.Reset();
    }

    public override void _ExitTree()
    {
        if (_context?.Health is not null)
            _context.Health.BeforeDied -= AoMorrer;
    }

    private void Aplicar(TransformationDefinition forma, FormRuntime runtime)
    {
        var contexto = _context ?? throw new InvalidOperationException("TransformationComponent precisa estar vinculada antes da ativação.");
        foreach (var modifier in runtime.Modifiers)
            contexto.Stats?.AddModifier(modifier);

        contexto.Movement?.SetExtraAirJumps(forma.ExtraAirJumps);
        contexto.Combat?.SetWeaponOverride(forma.WeaponOverride);

        var material = _formMaterial ?? throw new InvalidOperationException("O material da forma deve ser inicializado em _Ready.");
        material.AlbedoColor = forma.ThemeColor;
        material.EmissionEnabled = true;
        material.Emission = forma.ThemeColor;
        material.EmissionEnergyMultiplier = 0.45f;
        contexto.BodyBaseMaterial = material;
        if (_bodyMesh is not null)
            _bodyMesh.MaterialOverride = material;
        if (_cannonVisual is not null)
            _cannonVisual.Visible = false;

        if (_auraRing is not null && _auraMaterial is not null)
        {
            _auraMaterial.AlbedoColor = new Color(forma.ThemeColor.R, forma.ThemeColor.G, forma.ThemeColor.B, 0.42f);
            _auraMaterial.Emission = forma.ThemeColor;
            _auraRing.Visible = true;
            SetProcess(true);
        }

        ServiceLocator.Events.RaiseTransformationChanged(new TransformationPresentationEvent(
            forma.Id,
            contexto.Body.GlobalPosition,
            forma.ThemeColor,
            Activated: true));
    }

    private void GarantirAura(Node3D body)
    {
        if (_auraRing is not null)
            return;

        _auraMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(Colors.Cyan.R, Colors.Cyan.G, Colors.Cyan.B, 0.42f),
            EmissionEnabled = true,
            Emission = Colors.Cyan,
            EmissionEnergyMultiplier = 1.6f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _auraRing = new MeshInstance3D
        {
            Name = "AuraDaForma",
            Mesh = new TorusMesh { InnerRadius = 0.68f, OuterRadius = 0.88f, RingSegments = 24, Rings = 8 },
            MaterialOverride = _auraMaterial,
            Position = new Vector3(0f, 0.8f, 0f),
            Visible = false,
        };
        body.AddChild(_auraRing);
    }

    private void AoMorrer(Contenda.Components.Health.DamageInfo _) => Revert(RevertReason.Death);

    private sealed record FormRuntime(string Source, StatModifier[] Modifiers);
}
