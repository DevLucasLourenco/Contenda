using System;
using System.Text;
using Contenda.Components.Transformations;
using Contenda.Settings;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>Mostra seleção, forma ativa e dreno no HUD.</summary>
public sealed partial class TransformationSelector : Control
{
    [Export] public NodePath LabelPath { get; set; } = new("Texto");

    [Export] public NodePath FramePath { get; set; } = new("Painel");

    private Label? _label;
    private Panel? _frame;
    private TransformationComponent? _forms;
    private readonly StringBuilder _text = new();
    private bool _needsDraw;
    private bool _animarSelecao;
    private float _tempoPulso;
    private Vector2 _posicaoInicial;
    private Tween? _animacaoSelecao;

    public override void _Ready()
    {
        _label = GetNodeOrNull<Label>(LabelPath)
            ?? throw new InvalidOperationException("TransformationSelector: LabelPath não resolveu um Label.");
        _frame = GetNodeOrNull<Panel>(FramePath);
        _posicaoInicial = _label.Position;
    }

    public override void _ExitTree() => Unbind();

    public override void _Process(double delta)
    {
        _tempoPulso += (float)delta;

        if (!_needsDraw)
        {
            AtualizarMoldura();
            return;
        }

        _needsDraw = false;
        Desenhar();
        if (_animarSelecao)
        {
            _animarSelecao = false;
            AnimarSelecao();
        }
        AtualizarMoldura();
    }

    public void Bind(TransformationComponent forms)
    {
        ArgumentNullException.ThrowIfNull(forms);
        if (_label is null)
            throw new InvalidOperationException("TransformationSelector precisa estar pronto antes de Bind.");

        Unbind();
        _forms = forms;
        _forms.SelectionChanged += AoMudar;
        _forms.Activated += AoAtivar;
        _forms.Reverted += AoReverter;
        _needsDraw = true;
    }

    public void Unbind()
    {
        if (_forms is not null)
        {
            _forms.SelectionChanged -= AoMudar;
            _forms.Activated -= AoAtivar;
            _forms.Reverted -= AoReverter;
        }

        _forms = null;
        _needsDraw = true;
    }

    private void AoMudar(int _)
    {
        _needsDraw = true;
        _animarSelecao = true;
    }
    private void AoAtivar(TransformationDefinition _) => _needsDraw = true;
    private void AoReverter(TransformationDefinition _, RevertReason __) => _needsDraw = true;

    private void Desenhar()
    {
        if (_label is null)
            return;

        if (_forms is null)
        {
            _label.Text = string.Empty;
            return;
        }

        var text = _text.Clear().Append("◀  ");
        for (var slot = 0; slot <= _forms.Available.Count; slot++)
        {
            if (slot > 0)
                text.Append("  |  ");

            var name = slot == 0 ? "Normal" : _forms.Available[slot - 1].DisplayName;
            if (slot == _forms.SelectedIndex)
                text.Append("▶ [").Append(name).Append("] SELECIONADA");
            else
                text.Append(name);
        }

        if (_forms.Available.Count > 0)
            text.Append("  |  ???");
        text.Append("  ▶");
        if (_forms.Active is { } active)
            text.Append("\nATIVA: ").Append(active.DisplayName).Append(" · ").Append(active.ManaDrainPerSecond.ToString("0.#")).Append(" mana/s · ")
                .Append(SettingsStore.DescribeAction(Contenda.Core.InputActions.FormActivate)).Append(" para reverter");
        else
            text.Append("\nScroll: escolher · ").Append(SettingsStore.DescribeAction(Contenda.Core.InputActions.FormActivate)).Append(" : transformar");

        _label.Text = text.ToString();
    }

    private void AnimarSelecao()
    {
        if (_label is null)
            return;

        _animacaoSelecao?.Kill();
        _label.Position = _posicaoInicial - new Vector2(12f, 0f);
        _animacaoSelecao = CreateTween();
        _animacaoSelecao.TweenProperty(_label, "position", _posicaoInicial, 0.12f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }

    private void AtualizarMoldura()
    {
        if (_frame is null)
            return;

        if (_forms?.Active is { } active)
        {
            _frame.ThemeTypeVariation = "ActiveFormFrame";
            var brilho = 0.72f + (Mathf.Sin(_tempoPulso * 8f) + 1f) * 0.14f;
            _frame.SelfModulate = active.ThemeColor with { A = brilho };
        }
        else
        {
            _frame.ThemeTypeVariation = "HUDFrame";
            _frame.SelfModulate = Colors.White;
        }
    }
}
