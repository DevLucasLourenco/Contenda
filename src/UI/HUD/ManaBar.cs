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
/// O <see cref="HudController"/> liga as referências. A barra e o pulso são
/// apresentação em <c>_Process</c>; os textos são recalculados em chamada
/// adiada quando muda a mana ou a forma.
/// </remarks>
public sealed partial class ManaBar : Control
{
    private static readonly StringName ManaColor = new("mana");
    private static readonly StringName ManaLowColor = new("mana_low");
    private static readonly StringName HudPalette = new("HudPalette");

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
    private TransformationDefinition? _ultimaFormaDoStatus;
    private bool _ultimaLeituraManaBaixa;
    private bool _statusInicializado;
    private int _manaArredondadaAntes = -1;
    private int _maxArredondadoAntes = -1;
    private bool _textoAgendado;

    public override void _Ready()
    {
        _preenchimento = GetNodeOrNull<Control>(FillPath);
        _valor = GetNodeOrNull<Label>(ValueLabelPath);
        _status = GetNodeOrNull<Label>(StatusLabelPath);

        if (_preenchimento is null)
        {
            GD.PushError($"{Name}: FillPath não resolveu.");
            SetProcess(false);
        }
    }

    /// <summary>Recebe a mana e a forma ativa. Chamado pelo <see cref="HudController"/>.</summary>
    public void Bind(ManaComponent mana, TransformationComponent formas)
    {
        Unbind();
        _mana = mana;
        _formas = formas;
        _mana.ManaChanged += AoMudarMana;
        _formas.Activated += AoAtivarForma;
        _formas.Reverted += AoReverterForma;
        AgendarAtualizacaoTextos();
    }

    /// <summary>Libera as referências da partida encerrada.</summary>
    public void Unbind()
    {
        if (_mana is not null)
            _mana.ManaChanged -= AoMudarMana;

        if (_formas is not null)
        {
            _formas.Activated -= AoAtivarForma;
            _formas.Reverted -= AoReverterForma;
        }

        _mana = null;
        _formas = null;
        AgendarAtualizacaoTextos();
    }

    public override void _ExitTree() => Unbind();

    public override void _Process(double delta)
    {
        _tempo += (float)delta;
        if (_preenchimento is null || _mana is null)
            return;

        // Encolhe pela ESQUERDA -- mesma armadilha da HealthBar/WorldHealthBar:
        // escalar sozinho encolheria pelos dois lados a partir do centro.
        _preenchimento.Size = new Vector2(BarWidth * _mana.Percent, BarHeight);

        var formaAtiva = _formas?.Active;
        if (formaAtiva is null)
        {
            _preenchimento.SelfModulate = GetThemeColor(ManaColor, HudPalette);
            return;
        }

        var cor = formaAtiva.ThemeColor;
        if (_mana.Percent < 0.15f)
        {
            var pulso = 0.25f + (Mathf.Sin(_tempo * 8f) + 1f) * 0.375f;
            cor = cor.Lerp(GetThemeColor(ManaLowColor, HudPalette), pulso);
        }

        _preenchimento.SelfModulate = cor;
    }

    /// <summary>Fração exibida agora. Para o probe/depuração.</summary>
    public float CurrentFraction => _mana?.Percent ?? 0f;

    private void AoMudarMana(float atual, float maximo) => AgendarAtualizacaoTextos();

    private void AoAtivarForma(TransformationDefinition forma) => AgendarAtualizacaoTextos();

    private void AoReverterForma(TransformationDefinition forma, RevertReason motivo) => AgendarAtualizacaoTextos();

    private void AgendarAtualizacaoTextos()
    {
        if (_textoAgendado || !IsInsideTree())
            return;

        _textoAgendado = true;
        CallDeferred(nameof(AtualizarTextos));
    }

    private void AtualizarTextos()
    {
        _textoAgendado = false;

        if (_mana is null)
        {
            if (_valor is not null && _valor.Text.Length > 0)
                _valor.Text = string.Empty;
            if (_status is not null && _status.Text.Length > 0)
                _status.Text = string.Empty;

            _manaArredondadaAntes = -1;
            _maxArredondadoAntes = -1;
            _ultimaFormaDoStatus = null;
            _statusInicializado = false;
            return;
        }

        var manaAtual = Mathf.RoundToInt(_mana.Current);
        var manaMaxima = Mathf.RoundToInt(_mana.Max);
        if (_valor is not null && (manaAtual != _manaArredondadaAntes || manaMaxima != _maxArredondadoAntes))
            _valor.Text = $"{manaAtual}/{manaMaxima}";

        _manaArredondadaAntes = manaAtual;
        _maxArredondadoAntes = manaMaxima;

        var formaAtiva = _formas?.Active;
        var baixa = _mana.Percent < 0.15f;
        if (_status is not null && (!_statusInicializado
            || !ReferenceEquals(formaAtiva, _ultimaFormaDoStatus)
            || (formaAtiva is not null && baixa != _ultimaLeituraManaBaixa)))
        {
            _status.Text = formaAtiva is null
                ? string.Empty
                : baixa
                    ? "FORMA ATIVA · MANA BAIXA"
                    : $"FORMA ATIVA · DRENO {formaAtiva.ManaDrainPerSecond:0.#}/s";
        }

        _ultimaFormaDoStatus = formaAtiva;
        _ultimaLeituraManaBaixa = baixa;
        _statusInicializado = true;
    }
}
