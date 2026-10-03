using System;
using System.Collections.Generic;
using System.Text;
using Contenda.Characters.Base;
using Contenda.Components.Abilities;
using Contenda.Core;
using Contenda.Input;
using Contenda.Persistence;
using Contenda.Settings;
using Godot;

namespace Contenda.UI.Menus;

/// <summary>Escolha de arquétipo montada inteiramente do elenco e das definições.</summary>
public sealed partial class CharacterSelectMenu : Control
{
    [Export] public CharacterRosterDefinition? Roster { get; set; }

    [Export(PropertyHint.File, "*.tscn")]
    public string ArenaScenePath { get; set; } = "res://scenes/arena/HordeMatch.tscn";

    [Export(PropertyHint.File, "*.tscn")]
    public string MainMenuScenePath { get; set; } = "res://scenes/ui/menus/MainMenu.tscn";

    private readonly List<PanelContainer> _cards = [];
    private readonly List<Button> _selectButtons = [];
    private readonly List<(Button Button, Action Handler)> _selectHandlers = [];
    private readonly List<Node3D> _models = [];
    private CharacterRosterDefinition? _roster;
    private int _selectedIndex;
    private float _idleTime;
    private Button? _confirmButton;
    private Button? _backButton;

    /// <summary>Superfície de observação para a sonda da cena.</summary>
    public int CardCount => _cards.Count;
    public int SelectedIndex => _selectedIndex;
    public Button? BackButton => _backButton;
    public CharacterDefinition? SelectedDefinition => _roster is { } roster && _selectedIndex < roster.Characters.Length
        ? roster.Characters[_selectedIndex] : null;

    public override void _Ready()
    {
        if (Roster is not { Characters.Length: > 0 } roster)
            throw new InvalidOperationException($"{Name}: atribua um elenco não vazio.");

        _roster = roster;
        var profile = new ProfileStore(ProjectSettings.GlobalizePath(ServiceLocator.Session.ProfilePath)).Load();
        MontarTela(profile);
        Select(0);
    }

    public override void _Process(double delta)
    {
        _idleTime += (float)delta;
        foreach (var model in _models)
        {
            model.RotateY((float)delta * 0.28f);
            model.Position = new Vector3(0f, Mathf.Sin(_idleTime * 2f) * 0.035f, 0f);
        }
    }

    public override void _ExitTree()
    {
        foreach (var (button, handler) in _selectHandlers)
            button.Pressed -= handler;
        if (_confirmButton is not null)
            _confirmButton.Pressed -= Confirm;
        if (_backButton is not null)
            _backButton.Pressed -= Back;
    }

    public override void _Input(InputEvent input)
    {
        if (input.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Back();
        }
        else if (input.IsActionPressed("ui_left") || input.IsActionPressed(InputActions.MoveLeft))
        {
            GetViewport().SetInputAsHandled();
            Select((_selectedIndex - 1 + _cards.Count) % _cards.Count);
        }
        else if (input.IsActionPressed("ui_right") || input.IsActionPressed(InputActions.MoveRight))
        {
            GetViewport().SetInputAsHandled();
            Select((_selectedIndex + 1) % _cards.Count);
        }
        else if (input.IsActionPressed("ui_accept"))
        {
            // VOLTAR mantém o comportamento normal de Button com teclado.
            if (GetViewport().GuiGetFocusOwner() == _backButton)
                return;

            GetViewport().SetInputAsHandled();
            Confirm();
        }
    }

    /// <summary>Usado pelos botões e pelo teste de fluxo.</summary>
    public void Confirm()
    {
        if (SelectedDefinition is not { } selected)
            throw new InvalidOperationException("Nenhum personagem selecionado.");

        ServiceLocator.Session.SelectedCharacter = selected;
        ServiceLocator.Router.GoToAsync(ArenaScenePath);
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _cards.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _selectedIndex = index;
        var roster = _roster ?? throw new InvalidOperationException("Elenco não inicializado.");
        for (var i = 0; i < _cards.Count; i++)
            _cards[i].ThemeTypeVariation = i == index ? "SelectedCharacterCard" : "CharacterCard";

        _selectButtons[index].GrabFocus();
        if (_confirmButton is not null)
            _confirmButton.Text = $"JOGAR COM {roster.Characters[index].DisplayName.ToUpperInvariant()}";
    }

    private void MontarTela(ProfileData profile)
    {
        var background = new Panel { ThemeTypeVariation = "ScreenBackdrop", MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var page = new VBoxContainer();
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        page.AnchorLeft = 0.03f;
        page.AnchorTop = 0.03f;
        page.AnchorRight = 0.97f;
        page.AnchorBottom = 0.97f;
        AddChild(page);

        var title = new Label { Text = "ESCOLHA SEU PERSONAGEM", HorizontalAlignment = HorizontalAlignment.Center };
        title.ThemeTypeVariation = "SectionTitle";
        page.AddChild(title);

        var subtitle = new Label
        {
            Text = $"Veja as habilidades e a forma antes de entrar na arena.  {SettingsStore.DescribeAction(InputActions.MoveLeft)} / {SettingsStore.DescribeAction(InputActions.MoveRight)} ou ← / → para escolher.",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        subtitle.ThemeTypeVariation = "HudHint";
        page.AddChild(subtitle);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddChild(scroll);
        var cardRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        scroll.AddChild(cardRow);

        var roster = _roster ?? throw new InvalidOperationException("Elenco não inicializado.");
        foreach (var definition in roster.Characters)
        {
            if (definition is null || definition.ModelScene is null)
                throw new InvalidOperationException($"{Name}: personagem no elenco sem definição ou ModelScene.");

            var card = MontarCard(definition, profile);
            cardRow.AddChild(card);
            _cards.Add(card);
        }

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        page.AddChild(actions);

        _confirmButton = new Button { CustomMinimumSize = new Vector2(320, 52) };
        _confirmButton.Pressed += Confirm;
        actions.AddChild(_confirmButton);

        _backButton = new Button { Text = "VOLTAR", CustomMinimumSize = new Vector2(160, 52) };
        _backButton.Pressed += Back;
        actions.AddChild(_backButton);
    }

    private PanelContainer MontarCard(CharacterDefinition definition, ProfileData profile)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(475, 0),
            ThemeTypeVariation = "CharacterCard",
        };
        var padding = new MarginContainer();
        card.AddChild(padding);

        var column = new VBoxContainer();
        padding.AddChild(column);

        var name = new Label { Text = definition.DisplayName.ToUpperInvariant(), HorizontalAlignment = HorizontalAlignment.Center };
        name.ThemeTypeVariation = "CharacterName";
        column.AddChild(name);

        var preview = new SubViewportContainer { CustomMinimumSize = new Vector2(430, 210), Stretch = true };
        column.AddChild(preview);
        var viewport = new SubViewport
        {
            Size = new Vector2I(430, 210),
            OwnWorld3D = true,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        preview.AddChild(viewport);

        var stage = new Node3D();
        viewport.AddChild(stage);
        var modelScene = definition.ModelScene ?? throw new InvalidOperationException($"{definition.Id}: ModelScene ausente.");
        var model = modelScene.Instantiate<Node3D>();
        stage.AddChild(model);
        CharacterPresentation.BuildAnimationTree(model, definition.AnimationSet);
        CharacterPresentation.MountWeapon(model, definition.Weapon, definition.WeaponBoneName);
        _models.Add(model);

        var camera = new Camera3D { Current = true };
        stage.AddChild(camera);
        camera.LookAtFromPosition(new Vector3(3.4f, 2.2f, 4.6f), new Vector3(0, 1.2f, 0));
        stage.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-35, -30, 0), LightEnergy = 1.7f });
        stage.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.065f, 0.09f, 0.15f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.4f, 0.5f, 0.65f), AmbientLightEnergy = 0.8f } });

        var bio = new Label { Text = definition.Bio, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(430, 42) };
        column.AddChild(bio);

        AddRating(column, "DANO", definition.RatingDamage);
        AddRating(column, "ALCANCE", definition.RatingRange);
        AddRating(column, "VELOCIDADE", definition.RatingSpeed);
        AddRating(column, "RESISTÊNCIA", definition.RatingDurability);

        var abilitiesTitle = new Label { Text = "HABILIDADES  ·  sequência + " + SettingsStore.DescribeAction(InputActions.CommandConfirm) };
        abilitiesTitle.ThemeTypeVariation = "HudHint";
        column.AddChild(abilitiesTitle);

        foreach (var ability in definition.Abilities)
        {
            var row = new Label { Text = $"{SequenceText(ability)}  + {SettingsStore.DescribeAction(InputActions.CommandConfirm)}     {ability.DisplayName}" };
            row.ThemeTypeVariation = "HudHint";
            column.AddChild(row);
        }

        foreach (var transformation in definition.Transformations)
        {
            var form = new Label { Text = $"★ {transformation.DisplayName}: {transformation.Description}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(430, 48) };
            form.ThemeTypeVariation = "HudHint";
            column.AddChild(form);
        }

        var best = profile.Best.TryGetValue(definition.Id.ToString(), out var result)
            ? $"RECORDE: {result.Score:N0} pontos  ·  onda {result.Waves}"
            : "RECORDE: nenhuma partida";
        column.AddChild(new Label { Text = best });

        var index = _cards.Count;
        var choose = new Button { Text = "ESCOLHER", CustomMinimumSize = new Vector2(0, 42) };
        Action onChoose = () => Select(index);
        choose.Pressed += onChoose;
        _selectHandlers.Add((choose, onChoose));
        column.AddChild(choose);
        _selectButtons.Add(choose);
        return card;
    }

    private static void AddRating(VBoxContainer column, string title, int rating)
    {
        var row = new HBoxContainer();
        column.AddChild(row);
        row.AddChild(new Label { Text = title, CustomMinimumSize = new Vector2(125, 0) });
        var bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 5, Value = Mathf.Clamp(rating, 1, 5),
            ShowPercentage = false, CustomMinimumSize = new Vector2(255, 16),
        };
        row.AddChild(bar);
        row.AddChild(new Label { Text = $" {rating}/5" });
    }

    private static string SequenceText(AbilityDefinition ability)
    {
        var text = new StringBuilder();
        foreach (var token in ability.Sequence)
        {
            if (text.Length > 0)
                text.Append(' ');
            text.Append(token switch
            {
                CommandDirection.Up => SettingsStore.DescribeAction(InputActions.MoveUp),
                CommandDirection.Down => SettingsStore.DescribeAction(InputActions.MoveDown),
                CommandDirection.Left => SettingsStore.DescribeAction(InputActions.MoveLeft),
                CommandDirection.Right => SettingsStore.DescribeAction(InputActions.MoveRight),
                _ => "?",
            });
        }
        return text.ToString();
    }

    private void Back() => ServiceLocator.Router.GoToAsync(MainMenuScenePath);
}
