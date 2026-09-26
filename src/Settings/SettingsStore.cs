using System;
using System.Collections.Generic;
using System.IO;
using Contenda.Core;
using Contenda.Persistence;
using Godot;

namespace Contenda.Settings;

/// <summary>
/// A ponte entre <see cref="GameSettings"/> (dado puro, testado em xUnit) e a
/// engine: lê/grava `user://settings.cfg` e aplica o resultado no
/// `DisplayServer`, no `AudioServer` e no `InputMap`. Spec 14 §2.
/// </summary>
/// <remarks>
/// <see cref="Apply"/> é idempotente e roda no boot (`GameBootstrap`) antes da
/// primeira cena. <see cref="Commit"/> é o que o menu chama ao APLICAR: guarda em
/// <see cref="GameSession.Settings"/>, grava (atômico, ticket 29) e avisa por
/// evento -- salvar acontece ao aplicar, não a cada mexida de slider.
/// </remarks>
public static class SettingsStore
{
    public const string DefaultPath = "user://settings.cfg";

    private static readonly string[] BusNames = ["Master", "Music", "SFX", "UI", "Ambience"];

    // Os padrões do InputMap, capturados UMA vez antes de qualquer troca -- é
    // o que "restaurar padrões" e uma ação sem troca usam.
    private static Dictionary<string, Godot.Collections.Array<InputEvent>>? _padroes;

    /// <summary>Lê as configurações; arquivo ausente ou ilegível dá os padrões (spec 14: "apagar o arquivo abre com os padrões").</summary>
    public static GameSettings Load(string userPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(userPath);

        try
        {
            var real = ProjectSettings.GlobalizePath(userPath);
            return File.Exists(real) ? SettingsSerializer.Parse(File.ReadAllText(real)) : new GameSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushError($"SettingsStore: não consegui ler {userPath}: {e.Message}");
            return new GameSettings();
        }
    }

    public static void Save(string userPath, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            AtomicFile.WriteAllText(ProjectSettings.GlobalizePath(userPath), SettingsSerializer.Serialize(settings));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Perder a gravação é melhor que derrubar o menu; o valor ainda vale nesta sessão.
            GD.PushError($"SettingsStore: não consegui gravar {userPath}: {e.Message}");
        }
    }

    /// <summary>
    /// O que o jogo faz ao abrir: lê o arquivo (ausente = padrões) e aplica, ANTES
    /// de qualquer cena. Chamado pelo <c>GameBootstrap</c>.
    /// </summary>
    public static void LoadAndApply(GameSession sessao, SceneTree tree)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        ArgumentNullException.ThrowIfNull(tree);

        sessao.Settings = Load(sessao.SettingsPath);
        Apply(sessao.Settings, tree);
    }

    /// <summary>Guarda, grava, aplica e avisa. O caminho único de "aplicar" do menu.</summary>
    public static void Commit(GameSettings novo, SceneTree tree)
    {
        ArgumentNullException.ThrowIfNull(novo);
        ArgumentNullException.ThrowIfNull(tree);

        novo.Normalize();
        var sessao = ServiceLocator.Session;
        sessao.Settings = novo;

        Save(sessao.SettingsPath, novo);
        Apply(novo, tree);
        ServiceLocator.Events.RaiseSettingsChanged(new SettingsChangedEvent());
    }

    public static void Apply(GameSettings s, SceneTree tree)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(tree);

        CapturarPadroes();
        AplicarVideo(s.Video, tree);
        AplicarAudio(s.Audio);
        AplicarTeclas(s.Bindings);
    }

    // --- vídeo ---

    private static void AplicarVideo(VideoSettings v, SceneTree tree)
    {
        // Sem janela (headless, servidor dedicado) o DisplayServer recusa cada
        // chamada com um erro -- não há o que aplicar ali.
        if (DisplayServer.GetName() != "headless")
            AplicarJanela(v);

        Engine.MaxFps = v.FpsLimit;

        RenderingServer.DirectionalShadowAtlasSetSize(v.Shadows switch
        {
            ShadowQualityOption.Low => 1024,
            ShadowQualityOption.Medium => 2048,
            _ => 4096,
        }, is16Bits: true);

        tree.Root.Scaling3DScale = v.RenderScale;
    }

    private static void AplicarJanela(VideoSettings v)
    {
        switch (v.WindowMode)
        {
            case WindowModeOption.Windowed:
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                if (v.ResolutionWidth > 0 && v.ResolutionHeight > 0)
                    DisplayServer.WindowSetSize(new Vector2I(v.ResolutionWidth, v.ResolutionHeight));

                break;
            case WindowModeOption.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                break;
            case WindowModeOption.BorderlessFullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
        }

        DisplayServer.WindowSetVsyncMode(v.VSync switch
        {
            VSyncOption.Disabled => DisplayServer.VSyncMode.Disabled,
            VSyncOption.Adaptive => DisplayServer.VSyncMode.Adaptive,
            _ => DisplayServer.VSyncMode.Enabled,
        });
    }

    // --- áudio ---

    /// <summary>O volume atual do bus, em dB. Para o probe/depuração.</summary>
    public static float BusVolumeDb(string bus)
    {
        var i = AudioServer.GetBusIndex(bus);
        return i < 0 ? float.NaN : AudioServer.GetBusVolumeDb(i);
    }

    private static void AplicarAudio(AudioSettings a)
    {
        foreach (var nome in BusNames)
        {
            if (AudioServer.GetBusIndex(nome) >= 0)
                continue;

            AudioServer.AddBus();
            AudioServer.SetBusName(AudioServer.BusCount - 1, nome);
        }

        DefinirBus("Master", a.Master);
        DefinirBus("Music", a.Music);
        DefinirBus("SFX", a.Sfx);
        DefinirBus("UI", a.Ui);
        DefinirBus("Ambience", a.Ambience);
    }

    private static void DefinirBus(string nome, int volume)
        => AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(nome), volume <= 0 ? -80f : Mathf.LinearToDb(volume / 100f));

    // --- teclas ---

    /// <summary>
    /// As ações que o jogador pode remapear: todas as do `InputMap` menos as da
    /// interface (`ui_*`), a pausa e a ferramenta de debug. Spec 14 §1.
    /// </summary>
    public static List<string> RemappableActions()
    {
        CapturarPadroes();

        var acoes = new List<string>();
        foreach (var acao in Padroes().Keys)
        {
            if (!acao.StartsWith("ui_", StringComparison.Ordinal) && acao != InputActions.Pause.ToString() && !acao.StartsWith("debug_", StringComparison.Ordinal))
                acoes.Add(acao);
        }

        return acoes;
    }

    /// <summary>Os padrões de cada ação remapeável, no formato de <see cref="BindingSpec"/>.</summary>
    public static Dictionary<string, List<string>> DefaultBindings()
    {
        CapturarPadroes();

        var padroes = new Dictionary<string, List<string>>();
        foreach (var acao in RemappableActions())
        {
            var teclas = new List<string>();
            foreach (var evento in Padroes()[acao])
            {
                var spec = ToSpec(evento);
                if (spec is not null)
                    teclas.Add(spec);
            }

            padroes[acao] = teclas;
        }

        return padroes;
    }

    /// <remarks>
    /// Estado estático de propósito: o `InputMap` em si é global da engine, e o
    /// que precisa ser lembrado é o que ele tinha ANTES da primeira troca.
    /// Capturado uma vez, na primeira chamada -- nunca depois de uma troca.
    /// </remarks>
    private static Dictionary<string, Godot.Collections.Array<InputEvent>> Padroes()
    {
        if (_padroes is not null)
            return _padroes;

        var capturados = new Dictionary<string, Godot.Collections.Array<InputEvent>>();
        foreach (var acao in InputMap.GetActions())
            capturados[acao.ToString()] = InputMap.ActionGetEvents(acao);

        _padroes = capturados;
        return capturados;
    }

    private static void CapturarPadroes() => Padroes();

    private static void AplicarTeclas(Dictionary<string, List<string>> trocas)
    {
        foreach (var acao in RemappableActions())
        {
            var nome = new StringName(acao);
            InputMap.ActionEraseEvents(nome);

            if (trocas.TryGetValue(acao, out var teclas))
            {
                foreach (var spec in teclas)
                {
                    if (FromSpec(spec) is { } evento)
                        InputMap.ActionAddEvent(nome, evento);
                }

                // O que o menu não sabe remapear (um botão de controle, por exemplo)
                // continua valendo: trocar a tecla não apaga o resto do padrão.
                foreach (var padrao in Padroes()[acao])
                {
                    if (ToSpec(padrao) is null)
                        InputMap.ActionAddEvent(nome, padrao);
                }
            }
            else
            {
                foreach (var evento in Padroes()[acao])
                    InputMap.ActionAddEvent(nome, evento);
            }
        }
    }

    /// <summary>Uma tecla ou botão de mouse no formato estável. Nulo para o que o menu não sabe remapear.</summary>
    public static string? ToSpec(InputEvent evento) => evento switch
    {
        InputEventKey { PhysicalKeycode: not Key.None } k => BindingSpec.Key((int)k.PhysicalKeycode),
        InputEventMouseButton m => BindingSpec.Mouse((int)m.ButtonIndex),
        _ => null,
    };

    public static InputEvent? FromSpec(string spec)
    {
        var (tipo, valor) = BindingSpec.Split(spec);
        return tipo switch
        {
            "key" when valor > 0 => new InputEventKey { PhysicalKeycode = (Key)valor },
            "mouse" when valor > 0 => new InputEventMouseButton { ButtonIndex = (MouseButton)valor },
            _ => null,
        };
    }

    /// <summary>O texto de uma tecla para a tela ("W", "Botão direito"). O mesmo que a guia de combos usa.</summary>
    public static string Describe(string spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var (tipo, valor) = BindingSpec.Split(spec);
        switch (tipo)
        {
            case "key":
                // Sem teclado de verdade (headless) o DisplayServer recusa a consulta de layout.
                var tecla = DisplayServer.GetName() == "headless"
                    ? Key.None
                    : DisplayServer.KeyboardGetKeycodeFromPhysical((Key)valor);
                return OS.GetKeycodeString(tecla == Key.None ? (Key)valor : tecla);
            case "mouse":
                return ((MouseButton)valor) switch
                {
                    MouseButton.Left => "Mouse 1",
                    MouseButton.Right => "Mouse 2",
                    MouseButton.Middle => "Mouse 3",
                    _ => "Mouse " + valor,
                };
            default:
                return spec;
        }
    }

    /// <summary>O texto das teclas de uma ação AGORA, como o InputMap a vê (ou "-" se ninguém a usa).</summary>
    public static string DescribeAction(StringName acao)
    {
        var textos = new List<string>();
        foreach (var evento in InputMap.ActionGetEvents(acao))
        {
            if (ToSpec(evento) is { } spec)
                textos.Add(Describe(spec));
        }

        return textos.Count == 0 ? "-" : string.Join(" / ", textos);
    }
}
