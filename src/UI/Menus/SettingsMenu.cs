using System;
using System.Collections.Generic;
using Contenda.Core;
using Contenda.Settings;
using Godot;

namespace Contenda.UI.Menus;

/// <summary>
/// A tela de configurações -- a MESMA cena pelo menu principal e pelo pause
/// (overlay), em quatro abas: Vídeo, Áudio, Controles, Jogo. Spec 11 §3.4,
/// spec 14 §1, ticket 32.
/// </summary>
/// <remarks>
/// Edita um RASCUNHO (<see cref="Draft"/>), nunca o que está valendo: VOLTAR
/// descarta, APLICAR grava e aplica tudo de uma vez (<see cref="SettingsStore.Commit"/>).
/// Os controles são montados em código a partir dos dados -- a lista de teclas
/// vem do `InputMap` (`SettingsStore.RemappableActions`), então uma ação nova
/// aparece na aba Controles sem editar esta tela.
///
/// Remapear: clicar na tecla espera a próxima tecla/botão; se outra ação já a
/// usa, avisa e pede para repetir (a outra fica sem tecla, spec 14 §1).
/// </remarks>
public sealed partial class SettingsMenu : Control
{
    private static readonly string[] JanelaNomes = ["Janela", "Tela cheia", "Tela cheia sem borda"];
    private static readonly string[] VSyncNomes = ["Ligado", "Desligado", "Adaptativo"];
    private static readonly string[] SombraNomes = ["Baixa", "Média", "Alta"];
    private static readonly int[] LimitesDeFps = [60, 120, 144, 240, 0];
    private static readonly string[] JanelaDeComandosNomes = ["Curta (0,5 s)", "Normal (0,7 s)", "Longa (0,9 s)"];

    // Resoluções de lista fixa (o Godot 4 não expõe uma lista de modos por monitor, ao contrário do que a spec supõe).
    private static readonly Vector2I[] Resolucoes =
    [
        new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3840, 2160),
    ];

    [Export] public NodePath TabsPath { get; set; } = new();

    [Export] public NodePath ApplyButtonPath { get; set; } = new();

    [Export] public NodePath BackButtonPath { get; set; } = new();

    [Export] public NodePath StatusLabelPath { get; set; } = new();

    private TabContainer? _abas;
    private Button? _aplicar;
    private Button? _voltar;
    private Label? _status;

    private Dictionary<string, List<string>> _efetivo = new();
    private readonly Dictionary<string, Button> _botoesDeTecla = new();
    private string? _capturando;
    private (string Acao, string Spec)? _conflitoPendente;

    /// <summary>O rascunho em edição. Para o probe/depuração.</summary>
    public GameSettings Draft { get; private set; } = new();

    /// <summary>O botão de tecla de cada ação. Para o probe/depuração.</summary>
    public IReadOnlyDictionary<string, Button> KeyButtons => _botoesDeTecla;

    public string StatusText => _status?.Text ?? "";

    public event Action? BackRequested;

    public override void _Ready()
    {
        _abas = GetNodeOrNull<TabContainer>(TabsPath);
        _aplicar = GetNodeOrNull<Button>(ApplyButtonPath);
        _voltar = GetNodeOrNull<Button>(BackButtonPath);
        _status = GetNodeOrNull<Label>(StatusLabelPath);

        if (_abas is null || _aplicar is null || _voltar is null || _status is null)
        {
            GD.PushError($"{Name}: TabsPath, ApplyButtonPath, BackButtonPath ou StatusLabelPath não resolveram.");
            return;
        }

        // Títulos com acento: os nós se chamam Video/Audio para não carregar acento em caminho.
        _abas.SetTabTitle(0, "Vídeo");
        _abas.SetTabTitle(1, "Áudio");
        _abas.SetTabTitle(2, "Controles");
        _abas.SetTabTitle(3, "Jogo");

        Draft = ServiceLocator.Session.Settings.Clone();
        _efetivo = BindingSpec.Effective(SettingsStore.DefaultBindings(), Draft.Bindings);

        ConstruirVideo(Aba("Video"));
        ConstruirAudio(Aba("Audio"));
        ConstruirControles(Aba("Controles"));
        ConstruirJogo(Aba("Jogo"));

        _aplicar.Pressed += AplicarAgora;
        _voltar.Pressed += () => BackRequested?.Invoke();

        _aplicar.GrabFocus();
    }

    // `_abas` já foi verificado não nulo em _Ready, único lugar de onde isto é chamado.
    private VBoxContainer Aba(string nome) => _abas?.GetNode<VBoxContainer>(nome) ?? throw new InvalidOperationException("abas não resolvidas");

    public override void _Input(InputEvent evento)
    {
        if (_capturando is null || !evento.IsPressed() || evento is InputEventKey { Echo: true })
            return;

        string? spec = null;
        if (evento is InputEventKey tecla)
        {
            GetViewport().SetInputAsHandled();

            // ESC cancela a captura em vez de virar a tecla.
            if (tecla.PhysicalKeycode == Key.Escape)
            {
                CancelarCaptura();
                return;
            }

            spec = SettingsStore.ToSpec(tecla);
        }
        else if (evento is InputEventMouseButton mouse && mouse.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown
                     or MouseButton.WheelLeft or MouseButton.WheelRight))
        {
            GetViewport().SetInputAsHandled();
            spec = SettingsStore.ToSpec(mouse);
        }

        if (spec is not null && _capturando is { } acao)
        {
            _capturando = null;
            OfferBinding(acao, spec);
        }
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (!evento.IsActionPressed("ui_cancel"))
            return;

        GetViewport().SetInputAsHandled();
        BackRequested?.Invoke();
    }

    // --- controles ---

    /// <summary>
    /// Propõe <paramref name="spec"/> para <paramref name="acao"/>. Sem conflito, aplica no rascunho; com conflito,
    /// avisa e só aplica se a MESMA proposta for repetida.
    /// </summary>
    public BindingOffer OfferBinding(string acao, string spec)
    {
        var conflito = BindingSpec.FindConflict(_efetivo, acao, spec);
        if (conflito is not null && _conflitoPendente != (acao, spec))
        {
            _conflitoPendente = (acao, spec);
            Definir(_status, $"{SettingsStore.Describe(spec)} já é de \"{InputActionLabels.For(conflito)}\". Repita para confirmar -- ela ficará sem tecla.");
            AtualizarBotoesDeTecla();
            return BindingOffer.ConflictPending;
        }

        _conflitoPendente = null;
        var perdeu = BindingSpec.Rebind(_efetivo, acao, spec);

        Draft.Bindings[acao] = [spec];
        if (perdeu is not null)
            Draft.Bindings[perdeu] = [];

        Definir(_status, perdeu is null ? "" : $"\"{InputActionLabels.For(perdeu)}\" ficou sem tecla.");
        AtualizarBotoesDeTecla();
        return BindingOffer.Applied;
    }

    /// <summary>Volta todas as teclas ao padrão (no rascunho).</summary>
    public void RestoreDefaultBindings()
    {
        Draft.Bindings.Clear();
        _efetivo = BindingSpec.Effective(SettingsStore.DefaultBindings(), Draft.Bindings);
        _conflitoPendente = null;
        Definir(_status, "Teclas restauradas para o padrão.");
        AtualizarBotoesDeTecla();
    }

    /// <summary>Grava e aplica o rascunho. É o que o botão APLICAR faz.</summary>
    public void AplicarAgora()
    {
        SettingsStore.Commit(Draft.Clone(), GetTree());
        Definir(_status, "Configurações aplicadas.");
    }

    private void CancelarCaptura()
    {
        _capturando = null;
        AtualizarBotoesDeTecla();
    }

    private void AtualizarBotoesDeTecla()
    {
        foreach (var (acao, botao) in _botoesDeTecla)
        {
            botao.Text = _capturando == acao
                ? "Pressione uma tecla..."
                : _efetivo.TryGetValue(acao, out var teclas) && teclas.Count > 0
                    ? string.Join(" / ", teclas.ConvertAll(SettingsStore.Describe))
                    : "-";
        }
    }

    private void ConstruirControles(VBoxContainer aba)
    {
        var rolagem = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 360) };
        var lista = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rolagem.AddChild(lista);
        aba.AddChild(rolagem);

        foreach (var acao in SettingsStore.RemappableActions())
        {
            var linha = new HBoxContainer();
            linha.AddChild(new Label { Text = InputActionLabels.For(acao), CustomMinimumSize = new Vector2(320, 0) });

            var botao = new Button { CustomMinimumSize = new Vector2(280, 0) };
            var copia = acao;
            botao.Pressed += () =>
            {
                _capturando = copia;
                _conflitoPendente = null;
                AtualizarBotoesDeTecla();
            };

            linha.AddChild(botao);
            lista.AddChild(linha);
            _botoesDeTecla[acao] = botao;
        }

        var restaurar = new Button { Text = "RESTAURAR PADRÕES" };
        restaurar.Pressed += RestoreDefaultBindings;
        aba.AddChild(restaurar);

        AtualizarBotoesDeTecla();
    }

    // --- vídeo ---

    private void ConstruirVideo(VBoxContainer aba)
    {
        var v = Draft.Video;

        Opcao(aba, "Modo de janela", JanelaNomes, (int)v.WindowMode, i => v.WindowMode = (WindowModeOption)i);

        var resolucoes = new List<string> { "Atual" };
        var indiceDaResolucao = 0;
        for (var i = 0; i < Resolucoes.Length; i++)
        {
            resolucoes.Add($"{Resolucoes[i].X} x {Resolucoes[i].Y}");
            if (Resolucoes[i].X == v.ResolutionWidth && Resolucoes[i].Y == v.ResolutionHeight)
                indiceDaResolucao = i + 1;
        }

        Opcao(aba, "Resolução (em janela)", [.. resolucoes], indiceDaResolucao, i =>
        {
            v.ResolutionWidth = i == 0 ? 0 : Resolucoes[i - 1].X;
            v.ResolutionHeight = i == 0 ? 0 : Resolucoes[i - 1].Y;
        });

        Opcao(aba, "Sincronização vertical", VSyncNomes, (int)v.VSync, i => v.VSync = (VSyncOption)i);

        var nomesDeFps = Array.ConvertAll(LimitesDeFps, f => f == 0 ? "Ilimitado" : f.ToString());
        Opcao(aba, "Limite de quadros", nomesDeFps, Math.Max(0, Array.IndexOf(LimitesDeFps, v.FpsLimit)),
            i => v.FpsLimit = LimitesDeFps[i]);

        Opcao(aba, "Qualidade das sombras", SombraNomes, (int)v.Shadows, i => v.Shadows = (ShadowQualityOption)i);
        Slider(aba, "Escala de renderização", 0.5, 1.0, 0.05, v.RenderScale, x => v.RenderScale = (float)x);
    }

    // --- áudio ---

    private void ConstruirAudio(VBoxContainer aba)
    {
        var a = Draft.Audio;
        Slider(aba, "Geral", 0, 100, 1, a.Master, x => a.Master = (int)x);
        Slider(aba, "Música", 0, 100, 1, a.Music, x => a.Music = (int)x);
        Slider(aba, "Efeitos", 0, 100, 1, a.Sfx, x => a.Sfx = (int)x);
        Slider(aba, "Interface", 0, 100, 1, a.Ui, x => a.Ui = (int)x);
        Slider(aba, "Ambiente", 0, 100, 1, a.Ambience, x => a.Ambience = (int)x);
    }

    // --- jogo ---

    private void ConstruirJogo(VBoxContainer aba)
    {
        var g = Draft.Gameplay;
        Slider(aba, "Intensidade do tremor de tela (%)", 0, 150, 5, g.ShakeIntensityPercent, x => g.ShakeIntensityPercent = (int)x);
        Caixa(aba, "Mostrar números de dano", g.ShowDamageNumbers, x => g.ShowDamageNumbers = x);
        Caixa(aba, "Mostrar guia de combos", g.ShowComboGuide, x => g.ShowComboGuide = x);
        Opcao(aba, "Janela dos comandos (tolerância de tempo)", JanelaDeComandosNomes, (int)g.CommandWindow,
            i => g.CommandWindow = (CommandWindowOption)i);
    }

    // --- montagem de controles ---

    private static void Opcao(Container pai, string rotulo, string[] nomes, int selecionado, Action<int> aoMudar)
    {
        var linha = new HBoxContainer();
        linha.AddChild(new Label { Text = rotulo, CustomMinimumSize = new Vector2(360, 0) });

        var opcoes = new OptionButton { CustomMinimumSize = new Vector2(300, 0) };
        foreach (var nome in nomes)
            opcoes.AddItem(nome);

        opcoes.Selected = Math.Clamp(selecionado, 0, nomes.Length - 1);
        opcoes.ItemSelected += indice => aoMudar((int)indice);

        linha.AddChild(opcoes);
        pai.AddChild(linha);
    }

    private static void Slider(Container pai, string rotulo, double min, double max, double passo, double valor, Action<double> aoMudar)
    {
        var linha = new HBoxContainer();
        linha.AddChild(new Label { Text = rotulo, CustomMinimumSize = new Vector2(360, 0) });

        var slider = new HSlider
        {
            MinValue = min, MaxValue = max, Step = passo, Value = valor, CustomMinimumSize = new Vector2(300, 0),
        };
        var numero = new Label { Text = valor.ToString("0.##"), CustomMinimumSize = new Vector2(60, 0) };
        slider.ValueChanged += novo =>
        {
            numero.Text = novo.ToString("0.##");
            aoMudar(novo);
        };

        linha.AddChild(slider);
        linha.AddChild(numero);
        pai.AddChild(linha);
    }

    private static void Caixa(Container pai, string rotulo, bool ligado, Action<bool> aoMudar)
    {
        var caixa = new CheckBox { Text = rotulo, ButtonPressed = ligado };
        caixa.Toggled += ligadoAgora => aoMudar(ligadoAgora);
        pai.AddChild(caixa);
    }

    private static void Definir(Label? rotulo, string texto)
    {
        if (rotulo is not null)
            rotulo.Text = texto;
    }
}

/// <summary>O que aconteceu com uma proposta de tecla nova.</summary>
public enum BindingOffer
{
    /// <summary>Entrou no rascunho.</summary>
    Applied,

    /// <summary>Outra ação já usa a tecla -- repita para confirmar.</summary>
    ConflictPending,
}
