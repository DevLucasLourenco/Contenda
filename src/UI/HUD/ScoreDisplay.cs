using System;
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

    [Export] public NodePath WaveLabelPath { get; set; } = new();

    [Export] public NodePath TimeLabelPath { get; set; } = new();

    private Label? _pontos;
    private Label? _combo;
    private Label? _onda;
    private Label? _tempo;

    /// <summary>O texto de pontos mostrado agora. Para o probe/depuração.</summary>
    public string ScoreText => _pontos?.Text ?? "";

    /// <summary>O multiplicador mostrado agora (vazio sem combo). Para o probe/depuração.</summary>
    public string ComboText
    {
        get
        {
            var texto = _combo?.Text ?? "";
            return texto.StartsWith("COMBO  ", StringComparison.Ordinal) ? texto[7..] : texto;
        }
    }

    /// <summary>A onda atual exibida. Para o probe/depuração.</summary>
    public string WaveText => _onda?.Text ?? "";

    /// <summary>O relógio da partida exibido. Para o probe/depuração.</summary>
    public string TimeText => _tempo?.Text ?? "";

    public override void _Ready()
    {
        _pontos = GetNodeOrNull<Label>(ScoreLabelPath);
        _combo = GetNodeOrNull<Label>(ComboLabelPath);
        _onda = GetNodeOrNull<Label>(WaveLabelPath);
        _tempo = GetNodeOrNull<Label>(TimeLabelPath);

        if (_pontos is null || _combo is null || _onda is null || _tempo is null)
        {
            GD.PushError($"{Name}: algum caminho de placar, combo, onda ou tempo não resolveu.");
            return;
        }

        Visible = false;
        ServiceLocator.Events.ScoreChanged += AoMudarPlacar;
        ServiceLocator.Events.MatchStatusChanged += AoMudarStatus;
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.ScoreChanged -= AoMudarPlacar;
        ServiceLocator.Events.MatchStatusChanged -= AoMudarStatus;
    }

    private void AoMudarPlacar(ScoreChangedEvent evento)
    {
        if (_pontos is null || _combo is null || _onda is null || _tempo is null)
            return;

        Visible = true;
        _pontos.Text = "SCORE  " + FormatarPontos(evento.Score);

        // Só aparece quando há sequência de verdade: "x1,0" o tempo todo seria ruído.
        _combo.Text = evento.ComboMultiplier > 1.0001f
            ? "COMBO  x" + evento.ComboMultiplier.ToString("0.0", CultureInfo.InvariantCulture)
            : "";
    }

    private void AoMudarStatus(MatchStatusChangedEvent evento)
    {
        if (_onda is null || _tempo is null)
            return;

        _onda.Text = $"ONDA  {evento.Wave}";
        _tempo.Text = "TEMPO  " + FormatarDuracao(evento.ElapsedSeconds);
    }

    /// <summary>Milhar separado por espaço, como no mockup da spec 11: "4 250".</summary>
    public static string FormatarPontos(int pontos)
        => pontos.ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');

    /// <summary>Exibe minutos e segundos inteiros no relógio da partida.</summary>
    public static string FormatarDuracao(int segundos)
        => $"{Math.Max(0, segundos) / 60:00}:{Math.Max(0, segundos) % 60:00}";
}
