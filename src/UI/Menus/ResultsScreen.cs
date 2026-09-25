using System;
using System.Globalization;
using Contenda.Core;
using Contenda.UI.HUD;
using Godot;

namespace Contenda.UI.Menus;

/// <summary>
/// A tela de resultado: o que aconteceu na partida e o que fazer a seguir.
/// Spec 10 §9, spec 11, ticket 29.
/// </summary>
/// <remarks>
/// Vive no mesmo lugar do HUD -- um autoload (<c>GameBootstrap</c>) a adiciona à
/// raiz, fora da cena do nível (regra 1 do CLAUDE.md: nenhuma cena de
/// `scenes/ui/` dentro do mundo). Ouve <see cref="GameEvents.MatchEnded"/>; o
/// modo de jogo não sabe que ela existe.
///
/// "Trocar de personagem" e "Menu principal" levam a telas que ainda não existem
/// (tickets 30 e 31): o botão fica VISÍVEL e desabilitado enquanto o caminho não
/// existir no projeto, e liga sozinho quando a tela nascer -- mesma regra dos
/// modos futuros da spec 11 §1.
/// </remarks>
public sealed partial class ResultsScreen : CanvasLayer
{
    [Export] public NodePath TitleLabelPath { get; set; } = new();
    [Export] public NodePath ScoreLabelPath { get; set; } = new();
    [Export] public NodePath WavesLabelPath { get; set; } = new();
    [Export] public NodePath KillsLabelPath { get; set; } = new();
    [Export] public NodePath TimeLabelPath { get; set; } = new();
    [Export] public NodePath CharacterLabelPath { get; set; } = new();
    [Export] public NodePath RetryButtonPath { get; set; } = new();
    [Export] public NodePath ChangeCharacterButtonPath { get; set; } = new();
    [Export] public NodePath MainMenuButtonPath { get; set; } = new();
    [Export] public NodePath PanelPath { get; set; } = new();

    [Export(PropertyHint.File, "*.tscn")]
    public string CharacterSelectScenePath { get; set; } = "res://scenes/ui/menus/CharacterSelectMenu.tscn";

    [Export(PropertyHint.File, "*.tscn")]
    public string MainMenuScenePath { get; set; } = "res://scenes/ui/menus/MainMenu.tscn";

    private Control? _painel;
    private Label? _titulo;
    private Label? _pontos;
    private Label? _ondas;
    private Label? _abates;
    private Label? _tempo;
    private Label? _personagem;
    private Button? _tentarDeNovo;
    private Button? _trocarPersonagem;
    private Button? _menuPrincipal;

    /// <summary>Se a tela está visível agora. Para o probe/depuração.</summary>
    public bool IsShowing => _painel?.Visible ?? false;

    public Button? RetryButton => _tentarDeNovo;

    public Button? ChangeCharacterButton => _trocarPersonagem;

    public Button? MainMenuButton => _menuPrincipal;

    public string TitleText => _titulo?.Text ?? "";

    public string ScoreText => _pontos?.Text ?? "";

    public string WavesText => _ondas?.Text ?? "";

    public string KillsText => _abates?.Text ?? "";

    public string TimeText => _tempo?.Text ?? "";

    public string CharacterText => _personagem?.Text ?? "";

    public override void _Ready()
    {
        _painel = GetNodeOrNull<Control>(PanelPath);
        _titulo = GetNodeOrNull<Label>(TitleLabelPath);
        _pontos = GetNodeOrNull<Label>(ScoreLabelPath);
        _ondas = GetNodeOrNull<Label>(WavesLabelPath);
        _abates = GetNodeOrNull<Label>(KillsLabelPath);
        _tempo = GetNodeOrNull<Label>(TimeLabelPath);
        _personagem = GetNodeOrNull<Label>(CharacterLabelPath);
        _tentarDeNovo = GetNodeOrNull<Button>(RetryButtonPath);
        _trocarPersonagem = GetNodeOrNull<Button>(ChangeCharacterButtonPath);
        _menuPrincipal = GetNodeOrNull<Button>(MainMenuButtonPath);

        if (_painel is null || _titulo is null || _pontos is null || _ondas is null || _abates is null
            || _tempo is null || _personagem is null || _tentarDeNovo is null || _trocarPersonagem is null
            || _menuPrincipal is null)
        {
            GD.PushError($"{Name}: algum caminho de nó da tela de resultado não resolveu.");
            return;
        }

        _painel.Visible = false;

        _tentarDeNovo.Pressed += AoTentarDeNovo;
        _trocarPersonagem.Pressed += () => Ir(CharacterSelectScenePath);
        _menuPrincipal.Pressed += () => Ir(MainMenuScenePath);

        // Visível e desabilitado até a tela de destino existir.
        _trocarPersonagem.Disabled = !ResourceLoader.Exists(CharacterSelectScenePath);
        _menuPrincipal.Disabled = !ResourceLoader.Exists(MainMenuScenePath);
        if (_trocarPersonagem.Disabled)
            _trocarPersonagem.TooltipText = "Em breve";
        if (_menuPrincipal.Disabled)
            _menuPrincipal.TooltipText = "Em breve";

        ServiceLocator.Events.MatchEnded += AoTerminarAPartida;
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.MatchEnded -= AoTerminarAPartida;
    }

    private void AoTerminarAPartida(MatchEndedEvent evento)
    {
        if (_painel is null || _titulo is null || _pontos is null || _ondas is null || _abates is null
            || _tempo is null || _personagem is null || _tentarDeNovo is null)
        {
            return;
        }

        var resultado = evento.Result;

        _titulo.Text = resultado.Victory ? "VITÓRIA" : "DERROTA";
        _pontos.Text = "Pontos: " + ScoreDisplay.FormatarPontos(resultado.Score);
        _ondas.Text = "Ondas: " + resultado.WavesCleared.ToString(CultureInfo.InvariantCulture);
        _abates.Text = "Abates: " + resultado.EnemiesKilled.ToString(CultureInfo.InvariantCulture);
        _tempo.Text = "Tempo: " + FormatarTempo(resultado.DurationSeconds);
        _personagem.Text = "Personagem: " + resultado.CharacterId;

        _painel.Visible = true;
        _tentarDeNovo.GrabFocus();
    }

    private void AoTentarDeNovo()
    {
        Esconder();
        ServiceLocator.Router.Reload();
    }

    private void Ir(string caminho)
    {
        Esconder();
        ServiceLocator.Router.GoTo(caminho);
    }

    private void Esconder()
    {
        if (_painel is not null)
            _painel.Visible = false;
    }

    /// <summary>Segundos em "m:ss".</summary>
    public static string FormatarTempo(float segundos)
    {
        var total = Math.Max(0, (int)MathF.Round(segundos));
        return $"{total / 60}:{total % 60:00}";
    }
}
