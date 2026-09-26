using System;
using System.Text;
using Contenda.Components.Transformations;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>Mostra seleção, forma ativa e dreno no HUD.</summary>
public sealed partial class TransformationSelector : Control
{
    [Export] public NodePath LabelPath { get; set; } = new("Texto");

    private Label? _label;
    private TransformationComponent? _forms;
    private readonly StringBuilder _text = new();
    private bool _needsDraw;

    public override void _Ready()
    {
        _label = GetNodeOrNull<Label>(LabelPath)
            ?? throw new InvalidOperationException("TransformationSelector: LabelPath não resolveu um Label.");
    }

    public override void _ExitTree() => Unbind();

    public override void _Process(double delta)
    {
        if (!_needsDraw)
            return;

        _needsDraw = false;
        Desenhar();
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

    private void AoMudar(int _) => _needsDraw = true;
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
                text.Append('[').Append(name).Append(']');
            else
                text.Append(name);
        }

        if (_forms.Available.Count > 0)
            text.Append("  |  ???");
        text.Append("  ▶");
        if (_forms.Active is { } active)
            text.Append("\nATIVA: ").Append(active.DisplayName).Append(" · ").Append(active.ManaDrainPerSecond.ToString("0.#")).Append(" mana/s");
        else
            text.Append("\nScroll: selecionar · M3: ativar");

        _label.Text = text.ToString();
    }
}
