using Contenda.Components.Mana;
using Contenda.Components.Transformations;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>
/// Barra de mana do jogador.
/// </summary>
/// <remarks>
/// Sem camada de dano própria: o atraso da regeneração já é visível de graça
/// — a barra simplesmente não se move durante a pausa de
/// <see cref="ManaComponent"/>, porque <c>Percent</c> não muda enquanto ela
/// dura. Uma segunda camada aqui repetiria o que o ticket 12 já ganha do
/// <c>ManaState</c> sem custo nenhum.
///
/// Mesmas regras da <see cref="HealthBar"/>: não procura o jogador sozinha,
/// só o <see cref="HudController"/> chama <see cref="Bind"/>; lê por polling
/// em <c>_PhysicsProcess</c>, não em <c>_Process</c> — ver o comentário da
/// classe <see cref="HealthBar"/> sobre por que a spec 01 §6 é seguida em
/// espírito, não ao pé da letra, aqui.
/// </remarks>
public sealed partial class ManaBar : Control
{
    /// <summary>O preenchimento, que encolhe pela esquerda.</summary>
    [Export] public NodePath FillPath { get; set; } = new();

    /// <summary>Rótulo numérico opcional — "atual/máximo".</summary>
    [Export] public NodePath ValueLabelPath { get; set; } = new();

    [Export] public NodePath StatusLabelPath { get; set; } = new();

    /// <summary>Largura total da barra, em pixels. Precisa bater com a cena.</summary>
    [Export(PropertyHint.Range, "40,600,1")] public float BarWidth { get; set; } = 220f;

    /// <summary>Altura total da barra, em pixels.</summary>
    [Export(PropertyHint.Range, "4,60,1")] public float BarHeight { get; set; } = 18f;

    private Control? _preenchimento;
    private Label? _valor;
    private Label? _status;
    private ManaComponent? _mana;
    private TransformationComponent? _formas;
    private float _tempo;

    public override void _Ready()
    {
        _preenchimento = GetNodeOrNull<Control>(FillPath);
        _valor = GetNodeOrNull<Label>(ValueLabelPath);
        _status = GetNodeOrNull<Label>(StatusLabelPath);

        if (_preenchimento is null)
        {
            GD.PushError($"{Name}: FillPath não resolveu.");
            SetPhysicsProcess(false);
        }
    }

    /// <summary>Recebe a mana e a forma ativa. Chamado pelo <see cref="HudController"/>.</summary>
    public void Bind(ManaComponent mana, TransformationComponent formas)
    {
        _mana = mana;
        _formas = formas;
        Atualizar();
    }

    /// <summary>Libera as referências da partida encerrada.</summary>
    public void Unbind()
    {
        _mana = null;
        _formas = null;
        if (_status is not null)
            _status.Text = string.Empty;
    }

    public override void _PhysicsProcess(double delta)
    {
        _tempo += (float)delta;
        Atualizar();
    }

    private void Atualizar()
    {
        if (_mana is null || _preenchimento is null)
            return;

        // Encolhe pela ESQUERDA -- mesma armadilha da HealthBar/WorldHealthBar:
        // escalar sozinho encolheria pelos dois lados a partir do centro.
        _preenchimento.Size = new Vector2(BarWidth * _mana.Percent, BarHeight);

        if (_valor is not null)
            _valor.Text = $"{_mana.Current:0}/{_mana.Max:0}";

        var formaAtiva = _formas?.Active;
        if (formaAtiva is null)
        {
            _preenchimento.SelfModulate = GetThemeColor("mana", "HudPalette");
            if (_status is not null)
                _status.Text = string.Empty;
            return;
        }

        var baixa = _mana.Percent < 0.15f;
        var cor = formaAtiva.ThemeColor;
        if (baixa)
        {
            var pulso = 0.25f + (Mathf.Sin(_tempo * 8f) + 1f) * 0.375f;
            cor = cor.Lerp(GetThemeColor("mana_low", "HudPalette"), pulso);
        }

        _preenchimento.SelfModulate = cor;
        if (_status is not null)
            _status.Text = baixa
                ? "FORMA ATIVA · MANA BAIXA"
                : $"FORMA ATIVA · DRENO {formaAtiva.ManaDrainPerSecond:0.#}/s";
    }

    /// <summary>Fração exibida agora. Para o probe/depuração.</summary>
    public float CurrentFraction => _mana?.Percent ?? 0f;
}
