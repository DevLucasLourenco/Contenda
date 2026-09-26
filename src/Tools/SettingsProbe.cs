using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Contenda.Characters.Base;
using Contenda.Core;
using Contenda.Settings;
using Contenda.UI.HUD;
using Contenda.UI.Menus;
using Contenda.Vfx;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica que as configurações (ticket 32) chegam ao jogo de verdade: o
/// menu monta as abas a partir dos dados, aplicar muda o áudio, as opções de
/// jogo, o `InputMap` e a guia de combos, o arquivo grava e recarrega, e
/// apagar o arquivo dá os padrões.
/// </summary>
/// <remarks>
/// O formato do arquivo, os limites, os padrões e as contas de conflito de
/// teclas são xUnit (<c>SettingsSerializerTests</c>, <c>BindingSpecTests</c>);
/// aqui só o que exige a árvore e os servidores da engine. Grava em
/// `user://probe_settings.cfg`, nunca no arquivo de verdade.
///
/// Não dá para provar num headless que um volume é "audivelmente diferente" (não
/// há som ainda, e o `AudioServer` do headless é mudo) -- o que se confere é o
/// dB de cada bus.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/SettingsProbe.tscn
/// </code>
/// </remarks>
public sealed partial class SettingsProbe : Node
{
    private const string CaminhoDoProbe = "user://probe_settings.cfg";

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private string _caminhoAnterior = "";

    public override void _Ready()
    {
        var arena = GD.Load<PackedScene>("res://scenes/arena/Arena.tscn");
        AddChild(arena.Instantiate());
        _ = RodarComSeguranca();
    }

    private async Task RodarComSeguranca()
    {
        try
        {
            await Roteiro();
        }
        catch (System.Exception e)
        {
            Verificar(false, $"o roteiro lançou {e.GetType().Name}: {e.Message}");
        }

        // Deixa o InputMap como o resto dos probes espera e apaga o arquivo do probe.
        ServiceLocator.Session.SettingsPath = _caminhoAnterior;
        Concluir();
    }

    private async Task Roteiro()
    {
        var sessao = ServiceLocator.Session;
        _caminhoAnterior = sessao.SettingsPath;
        sessao.SettingsPath = CaminhoDoProbe;
        Apagar();

        await Quadros(60);
        _jogador = sessao.PlayerBody;
        var guia = Encontrar<AbilityGuide>(GetTree().Root);
        var habilidades = _jogador?.Context?.Abilities;
        var numeros = GetNodeOrNull<DamageNumberPool>("/root/DamageNumberPool");
        Verificar(guia is not null && habilidades is not null && numeros is not null,
            "guia de combos, habilidades do jogador ou pool de números de dano não resolveram");
        if (guia is null || habilidades is null || numeros is null)
            return;

        // Ponto de partida: os padrões, o que o boot sem arquivo produz.
        sessao.Settings = new GameSettings();
        SettingsStore.Apply(sessao.Settings, GetTree());
        var confirmarPadrao = SettingsStore.DescribeAction(InputActions.CommandConfirm);
        Verificar(confirmarPadrao != "-", "command_confirm deveria ter uma tecla por padrão");

        // --- o menu é montado a partir dos dados ---
        var cena = GD.Load<PackedScene>("res://scenes/ui/menus/SettingsMenu.tscn");
        var menu = cena.Instantiate<SettingsMenu>();
        AddChild(menu);
        await Quadros(3);

        var abas = menu.GetNode<TabContainer>(menu.TabsPath);
        Verificar(abas.GetChildCount() == 4, $"deveria haver 4 abas; há {abas.GetChildCount()}");

        var acoes = SettingsStore.RemappableActions();
        Verificar(acoes.Count >= 10, $"deveria haver as ações do jogo para remapear; há {acoes.Count}");
        Verificar(menu.KeyButtons.Count == acoes.Count, "a aba Controles deveria ter um botão por ação remapeável");
        Verificar(!acoes.Exists(a => a.StartsWith("ui_") || a == "pause" || a.StartsWith("debug_")),
            "pause, ui_* e debug não deveriam ser remapeáveis");

        // --- áudio: volumes separados, dBs diferentes ---
        menu.Draft.Audio.Master = 50;
        menu.Draft.Audio.Music = 0;
        menu.Draft.Audio.Sfx = 100;
        menu.Draft.Audio.Ui = 25;
        menu.AplicarAgora();

        Verificar(SettingsStore.BusVolumeDb("Music") <= -79f, "volume 0 deveria silenciar o bus de música");
        Verificar(Mathf.Abs(SettingsStore.BusVolumeDb("Master") - Mathf.LinearToDb(0.5f)) < 0.05f, "Master a 50% deveria ser ~-6 dB");
        Verificar(Mathf.Abs(SettingsStore.BusVolumeDb("SFX")) < 0.05f, "SFX a 100% deveria ser 0 dB");
        Verificar(SettingsStore.BusVolumeDb("UI") < SettingsStore.BusVolumeDb("SFX"), "os volumes deveriam ser independentes por bus");
        Verificar(File.Exists(ProjectSettings.GlobalizePath(CaminhoDoProbe)), "aplicar deveria gravar o arquivo de configurações");

        // --- vídeo: só onde há janela de verdade (o headless não tem DisplayServer para aplicar) ---
        menu.Draft.Video.FpsLimit = 144;
        menu.Draft.Video.RenderScale = 0.75f;
        menu.Draft.Video.VSync = VSyncOption.Disabled;
        menu.Draft.Video.WindowMode = WindowModeOption.Windowed;
        menu.Draft.Video.ResolutionWidth = 1366;
        menu.Draft.Video.ResolutionHeight = 768;
        menu.AplicarAgora();
        await Quadros(10);

        Verificar(Engine.MaxFps == 144, $"o limite de quadros deveria ser 144; é {Engine.MaxFps}");
        Verificar(Mathf.Abs(GetTree().Root.Scaling3DScale - 0.75f) < 0.001f, "a escala de renderização deveria ser 0,75");
        if (DisplayServer.GetName() != "headless")
        {
            Verificar(DisplayServer.WindowGetVsyncMode() == DisplayServer.VSyncMode.Disabled, "o vsync deveria estar desligado");
            Verificar(DisplayServer.WindowGetSize() == new Vector2I(1366, 768),
                $"a janela deveria ter 1366x768; tem {DisplayServer.WindowGetSize()}");
        }

        menu.Draft.Video.FpsLimit = 0;
        menu.Draft.Video.RenderScale = 1f;
        menu.Draft.Video.VSync = VSyncOption.Enabled;
        menu.Draft.Video.ResolutionWidth = 0;
        menu.Draft.Video.ResolutionHeight = 0;
        menu.AplicarAgora();

        // --- jogo: números de dano, guia, janela de comandos, tremor ---
        var janelaNormal = habilidades.CommandWindowSeconds;
        Verificar(Mathf.Abs(janelaNormal - 0.7f) < 0.001f, $"a janela padrão deveria ser 0,7 s; é {janelaNormal}");

        menu.Draft.Gameplay.ShowDamageNumbers = false;
        menu.Draft.Gameplay.ShowComboGuide = false;
        menu.Draft.Gameplay.CommandWindow = CommandWindowOption.Short;
        menu.Draft.Gameplay.ShakeIntensityPercent = 0;
        menu.AplicarAgora();

        Verificar(!guia.Visible, "desligar a guia de combos deveria escondê-la");
        Verificar(Mathf.Abs(habilidades.CommandWindowSeconds - 0.5f) < 0.001f,
            $"a janela curta deveria ser 0,5 s; é {habilidades.CommandWindowSeconds}");
        Verificar(sessao.Settings.Gameplay.ShakeMultiplier == 0f, "tremor a 0% deveria zerar o multiplicador");

        var ativosAntes = numeros.ActiveCount;
        ServiceLocator.Events.RaiseDamageNumber(new DamageNumberEvent(Vector3.Zero, 10f, false));
        Verificar(numeros.ActiveCount == ativosAntes, "com os números de dano desligados, nenhum deveria nascer");

        menu.Draft.Gameplay.ShowDamageNumbers = true;
        menu.Draft.Gameplay.ShowComboGuide = true;
        menu.Draft.Gameplay.CommandWindow = CommandWindowOption.Long;
        menu.Draft.Gameplay.ShakeIntensityPercent = 100;
        menu.AplicarAgora();
        Verificar(guia.Visible, "religar a guia deveria mostrá-la");
        Verificar(Mathf.Abs(habilidades.CommandWindowSeconds - 0.9f) < 0.001f, "a janela longa deveria ser 0,9 s");
        ServiceLocator.Events.RaiseDamageNumber(new DamageNumberEvent(Vector3.Zero, 10f, false));
        Verificar(numeros.ActiveCount > ativosAntes, "com os números de dano ligados, eles deveriam nascer de novo");

        // --- teclas: rebind, conflito, guia de combos acompanha, restaurar ---
        Verificar(menu.OfferBinding("dash", BindingSpec.Key((int)Key.E)) == BindingOffer.Applied, "tecla livre deveria entrar direto");
        menu.AplicarAgora();
        Verificar(Tem("dash", Key.E), "o InputMap deveria ter a nova tecla do dash");
        Verificar(!Tem("dash", Key.Shift), "a tecla antiga do dash deveria ter sido substituída, não somada");

        Verificar(guia.ConfirmKeyText == confirmarPadrao, "a guia deveria mostrar a tecla de confirmar padrão");
        Verificar(menu.OfferBinding("command_confirm", BindingSpec.Key((int)Key.F)) == BindingOffer.Applied, "F está livre");
        menu.AplicarAgora();
        Verificar(guia.ConfirmKeyText == "F", $"remapear a confirmação deveria refletir na guia; mostra \"{guia.ConfirmKeyText}\"");

        var conflito = menu.OfferBinding("jump", BindingSpec.Key((int)Key.F));
        Verificar(conflito == BindingOffer.ConflictPending, "F já é da confirmação: deveria avisar do conflito");
        Verificar(menu.StatusText.Contains("Confirmar habilidade"), $"o aviso deveria dizer quem perde a tecla; diz \"{menu.StatusText}\"");
        Verificar(menu.OfferBinding("jump", BindingSpec.Key((int)Key.F)) == BindingOffer.Applied, "repetir deveria confirmar");
        menu.AplicarAgora();
        Verificar(Tem("jump", Key.F), "o pulo deveria ter ficado com F");
        Verificar(InputMap.ActionGetEvents(InputActions.CommandConfirm).Count == 0, "a confirmação deveria ter ficado sem tecla");
        Verificar(guia.ConfirmKeyText == "-", "a guia deveria mostrar que a confirmação está sem tecla");

        // --- captura de tecla por eventos REAIS (o caminho do botão até _Input) ---
        var botao = menu.KeyButtons["dash"];
        botao.EmitSignal(BaseButton.SignalName.Pressed);
        Verificar(botao.Text.Contains("Pressione"), "clicar na tecla deveria esperar a próxima tecla");
        Godot.Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
        await Quadros(10);
        Verificar(botao.Text.Contains("Pressione"), "a roda do mouse não deveria virar tecla");
        Godot.Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });
        await AteQue(() => !botao.Text.Contains("Pressione"));
        Verificar(!botao.Text.Contains("Pressione") && Tem("dash", Key.E), "ESC deveria cancelar a captura sem mudar a tecla");
        botao.EmitSignal(BaseButton.SignalName.Pressed);
        Godot.Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.R, Pressed = true });
        await AteQue(() => botao.Text == "R");
        Verificar(menu.Draft.Bindings.TryGetValue("dash", out var dashR) && dashR.Contains(BindingSpec.Key((int)Key.R)),
            "apertar uma tecla durante a captura deveria trocar a ação no rascunho");
        Verificar(botao.Text == "R", $"o botão deveria mostrar a tecla nova; mostra \"{botao.Text}\"");
        menu.OfferBinding("dash", BindingSpec.Key((int)Key.E));
        menu.AplicarAgora();

        // --- abrir o jogo de novo, de verdade: LoadAndApply (o que o GameBootstrap chama) sobre um arquivo gravado ---
        sessao.Settings = new GameSettings();
        SettingsStore.Apply(sessao.Settings, GetTree());
        Verificar(!Tem("dash", Key.E), "antes do \"boot\", o InputMap deveria estar nos padrões");
        SettingsStore.LoadAndApply(sessao, GetTree());
        Verificar(Tem("dash", Key.E) && Tem("jump", Key.F), "o boot deveria aplicar as teclas gravadas");
        Verificar(Mathf.Abs(SettingsStore.BusVolumeDb("Master") - Mathf.LinearToDb(0.5f)) < 0.05f, "o boot deveria aplicar o volume gravado");
        Verificar(sessao.Settings.Audio.Master == 50, "o boot deveria deixar as configurações gravadas na sessão");

        // --- fechar e reabrir: o que foi gravado volta; apagar o arquivo volta aos padrões ---
        var gravado = SettingsStore.Load(CaminhoDoProbe);
        Verificar(gravado.Audio.Master == 50 && gravado.Audio.Music == 0, "o arquivo deveria guardar o áudio");
        Verificar(gravado.Bindings.ContainsKey("dash") && gravado.Bindings.ContainsKey("jump"), "o arquivo deveria guardar as teclas trocadas");

        sessao.Settings = new GameSettings();
        SettingsStore.Apply(sessao.Settings, GetTree()); // simula o boot de outra execução
        Verificar(Tem("dash", Key.Shift), "sem configurações, o dash deveria voltar ao padrão");

        sessao.Settings = SettingsStore.Load(CaminhoDoProbe);
        SettingsStore.Apply(sessao.Settings, GetTree());
        Verificar(Tem("dash", Key.E) && Tem("jump", Key.F), "reabrir deveria trazer as teclas gravadas");
        Verificar(Mathf.Abs(SettingsStore.BusVolumeDb("Master") - Mathf.LinearToDb(0.5f)) < 0.05f, "reabrir deveria trazer o volume gravado");

        Apagar();
        var padrao = SettingsStore.Load(CaminhoDoProbe);
        Verificar(padrao.Audio.Master == 100 && padrao.Bindings.Count == 0, "apagar o arquivo deveria dar os padrões");
        sessao.Settings = padrao;
        SettingsStore.Apply(padrao, GetTree());
        Verificar(Tem("dash", Key.Shift) && InputMap.ActionGetEvents(InputActions.CommandConfirm).Count > 0,
            "com os padrões, as teclas originais deveriam voltar");
        // Apply sozinho (o do boot) não avisa ninguém -- a guia relê quando o aviso chega.
        ServiceLocator.Events.RaiseSettingsChanged(new SettingsChangedEvent());
        Verificar(guia.ConfirmKeyText == confirmarPadrao, "e a guia deveria voltar a mostrar a confirmação padrão");

        // restaurar padrões pelo menu
        menu.OfferBinding("dash", BindingSpec.Key((int)Key.Q));
        menu.RestoreDefaultBindings();
        menu.AplicarAgora();
        Verificar(Tem("dash", Key.Shift), "\"restaurar padrões\" deveria devolver a tecla original");

        // arquivo corrompido não quebra a abertura
        File.WriteAllText(ProjectSettings.GlobalizePath(CaminhoDoProbe), "\0\0 lixo [[[ ===");
        Verificar(SettingsStore.Load(CaminhoDoProbe).Audio.Master == 100, "arquivo ilegível deveria dar os padrões");
    }

    private static bool Tem(string acao, Key tecla)
    {
        foreach (var evento in InputMap.ActionGetEvents(new StringName(acao)))
        {
            if (evento is InputEventKey k && k.PhysicalKeycode == tecla)
                return true;
        }

        return false;
    }

    private static void Apagar()
    {
        var real = ProjectSettings.GlobalizePath(CaminhoDoProbe);
        if (File.Exists(real))
            File.Delete(real);

        if (File.Exists(real + ".tmp"))
            File.Delete(real + ".tmp");
    }

    /// <summary>Espera a condição valer (até ~1,5 s de jogo): entrada injetada nem sempre chega no mesmo quadro.</summary>
    private async Task AteQue(System.Func<bool> condicao)
    {
        for (var i = 0; i < 90 && !condicao(); i++)
            await Quadros(1);
    }

    private async Task Quadros(int quantos)
    {
        for (var i = 0; i < quantos; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private static T? Encontrar<T>(Node no) where T : Node
    {
        if (no is T achado)
            return achado;

        foreach (var filho in no.GetChildren())
        {
            var dentro = Encontrar<T>(filho);
            if (dentro is not null)
                return dentro;
        }

        return null;
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[config] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        Apagar();

        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[config] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[config] todas as verificações passaram");
        GetTree().Quit();
    }
}
