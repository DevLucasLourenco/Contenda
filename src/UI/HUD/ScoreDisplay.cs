using System.Globalization;
using Contenda.Core;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>O placar e o multiplicador de combo, no canto da tela, reagindo na hora.</summary>
/// <remarks>
/// Ouve <see cref="GameEvents.ScoreChanged"/> sozinho, mesmo desenho de
/// <see cref="WaveBanner"/> -- "todos alimentados por eventos do modo, nunca por
/// polling da cena" (spec 11 §2.5). Nada aqui lê o placar de volta do modo.
/// Nasce escondido: só há placar depois que uma partida começa.
/// </remarks>
public sealed partial class ScoreDisplay : Control
{
    [Export] public NodePath ScoreLabelPath { get; set; } = new();

    [Export] public NodePath ComboLabelPath { get; set; } = new();

    private Label? _pontos;
    private Label? _combo;

    /// <summary>O texto de pontos mostrado agora. Para o probe/depuração.</summary>
    public string ScoreText => _pontos?.Text ?? "";

    /// <summary>O texto do combo mostrado agora (vazio sem combo). Para o probe/depuração.</summary>
    public string ComboText => _combo?.Text ?? "";

    public override void _Ready()
    {
        _pontos = GetNodeOrNull<Label>(ScoreLabelPath);
        _combo = GetNodeOrNull<Label>(ComboLabelPath);

        if (_pontos is null || _combo is null)
        {
            GD.PushError($"{Name}: ScoreLabelPath ou ComboLabelPath não resolveram.");
            return;
        }

        Visible = false;
        ServiceLocator.Events.ScoreChanged += AoMudarPlacar;
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.ScoreChanged -= AoMudarPlacar;
    }

    private void AoMudarPlacar(ScoreChangedEvent evento)
    {
        if (_pontos is null || _combo is null)
            return;

        Visible = true;
        _pontos.Text = "SCORE  " + FormatarPontos(evento.Score);

        // Só aparece quando há sequência de verdade: "x1,0" o tempo todo seria ruído.
        _combo.Text = evento.ComboMultiplier > 1.0001f
            ? "x" + evento.ComboMultiplier.ToString("0.0", CultureInfo.InvariantCulture)
            : "";
    }

    /// <summary>Milhar separado por espaço, como no mockup da spec 11: "4 250".</summary>
    public static string FormatarPontos(int pontos)
        => pontos.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
}
