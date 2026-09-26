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

    public override void _Ready() => _label = GetNodeOrNull<Label>(LabelPath);

    public void Bind(TransformationComponent forms)
    {
        Unbind();
        _forms = forms;
        _forms.SelectionChanged += AoMudar;
        _forms.Activated += AoAtivar;
        _forms.Reverted += AoReverter;
        Desenhar();
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
        if (_label is not null)
            _label.Text = string.Empty;
    }

    private void AoMudar(int _) => Desenhar();
    private void AoAtivar(TransformationDefinition _) => Desenhar();
    private void AoReverter(TransformationDefinition _, RevertReason __) => Desenhar();

    private void Desenhar()
    {
        if (_label is null || _forms is null)
            return;

        var text = new StringBuilder("◀  ");
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
