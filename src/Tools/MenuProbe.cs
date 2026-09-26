using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Contenda.Core;
using Contenda.UI.HUD;
using Contenda.UI.Menus;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o menu principal, a escolha de modo, a tela de carregamento e a
/// volta da partida ao menu na árvore real (ticket 30).
/// </summary>
/// <remarks>
/// O probe precisa SOBREVIVER às trocas de cena que ele mesmo provoca: a cena
/// que o Godot abre (<c>MenuProbe.tscn</c>) só cria um "piloto" (esta mesma
/// classe com <see cref="_ehPiloto"/>) pendurado na raiz da árvore e troca para
/// o menu -- o piloto é quem roda o roteiro.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/MenuProbe.tscn
/// </code>
/// </remarks>
public sealed partial class MenuProbe : Node
{
    private const string MenuPath = "res://scenes/ui/menus/MainMenu.tscn";
    private const int CiclosDeNavegacao = 20;
    private const int VoltasMenuPartidaMenu = 4;

    private bool _ehPiloto;
    private readonly List<string> _falhas = [];
    private readonly List<SceneLoadProgressEvent> _andamento = [];
    private bool _telaDeCarregamentoApareceu;

    public override void _Ready()
    {
        if (_ehPiloto)
        {
            ServiceLocator.Events.SceneLoadProgress += AoAndarOCarregamento;
            _ = RodarComSeguranca();
            return;
        }

        // Cena de lançamento: entrega o roteiro a um piloto na raiz e vai para o menu.
        var piloto = new MenuProbe { _ehPiloto = true, Name = "MenuProbePiloto" };
        GetTree().Root.CallDeferred(Node.MethodName.AddChild, piloto);
        CallDeferred(nameof(IrParaOMenu));
    }

    public override void _ExitTree()
    {
        if (_ehPiloto)
            ServiceLocator.Events.SceneLoadProgress -= AoAndarOCarregamento;
    }

    private void IrParaOMenu() => GetTree().ChangeSceneToFile(MenuPath);

    private async Task RodarComSeguranca()
    {
        try
        {
            await Roteiro();
        }
        catch (Exception e)
        {
            Verificar(false, $"o roteiro do probe lançou {e.GetType().Name}: {e.Message}");
        }

        Concluir();
    }

    private async Task Roteiro()
    {
        // Um perfil PRÓPRIO do probe: as partidas que ele joga (e perde) não sujam o de verdade.
        var perfilDoProbe = ProjectSettings.GlobalizePath("user://probe_menu_profile.cfg");
        if (File.Exists(perfilDoProbe))
            File.Delete(perfilDoProbe);

        ServiceLocator.Session.ProfilePath = "user://probe_menu_profile.cfg";

        // --- o menu abre, com a cidade ao fundo e sem HUD ---
        await AteACenaSer("MainMenu");
        await Quadros(10);

        var menu = (MainMenu)GetTree().CurrentScene;
        var backdrop = menu.Backdrop!; // MainMenu resolve BackdropPath em cena de menu; nulo aqui seria falha do probe.

        Verificar(menu.StartButton is not null && menu.SettingsButton is not null && menu.QuitButton is not null,
            "o menu deveria oferecer iniciar, configurações e sair");
        Verificar(menu.SettingsButton?.Disabled == !ResourceLoader.Exists(menu.SettingsScenePath),
            "configurações só deveria estar habilitado quando a tela de configurações existir");
        Verificar(GetViewport().GuiGetFocusOwner() == menu.StartButton, "o foco inicial deveria estar em INICIAR (teclado sem mouse)");

        Verificar(backdrop.BackdropCamera is { Current: true }, "o fundo deveria ter uma câmera ativa");
        Verificar(GetViewport().GetCamera3D() == backdrop.BackdropCamera, "a câmera do fundo deveria ser a que renderiza");
        Verificar(ServiceLocator.Session.PlayerBody is null, "o fundo do menu não deveria ter jogador");
        Verificar(!ExisteJogadorNaArvore(), "nenhum personagem de jogador deveria estar vivo no fundo do menu");

        var hud = Encontrar<HudController>(GetTree().Root);
        Verificar(hud is { Visible: false, BoundPlayer: null }, "o HUD não deveria aparecer no menu");

        var posicaoAntes = backdrop.BackdropCamera?.Position ?? Vector3.Zero;
        await Quadros(60);
        Verificar((backdrop.BackdropCamera?.Position ?? Vector3.Zero).DistanceTo(posicaoAntes) > 0.01f,
            "a câmera do fundo deveria estar em órbita");

        // --- configurações pelo menu principal (a mesma cena do pause) ---
        Verificar(menu.SettingsButton is { Disabled: false }, "com a tela de configurações existindo, o botão deveria estar habilitado");
        menu.SettingsButton!.EmitSignal(BaseButton.SignalName.Pressed);
        await Quadros(3);
        Verificar(menu.OpenSettings is not null && !menu.IsMainPanelShowing, "CONFIGURAÇÕES deveria abrir a tela de ajustes por cima");
        var cenaDoMenu = menu.SettingsMenuScene?.ResourcePath;
        Apertar("ui_cancel");
        await AteQue(() => menu.OpenSettings is null);
        await Quadros(2);
        Verificar(menu.OpenSettings is null && menu.IsMainPanelShowing, "ESC deveria fechar as configurações e voltar ao menu");
        Verificar(GetViewport().GuiGetFocusOwner() == menu.SettingsButton, "voltar das configurações deveria devolver o foco ao botão");

        // --- iniciar leva à escolha de modo; teclado; voltar ---
        // Enter no botão que tem o foco, não um sinal: prova foco + ação juntos.
        // (Depois de voltar das configurações o foco está nelas -- sobe para INICIAR pelo teclado.)
        Apertar("ui_up");
        await Quadros(2);
        Verificar(GetViewport().GuiGetFocusOwner() == menu.StartButton, "seta para cima de CONFIGURAÇÕES deveria ir para INICIAR");
        Apertar("ui_accept");
        await Quadros(3);

        var modos = menu.OpenModeMenu;
        Verificar(modos is not null, "INICIAR deveria abrir a escolha de modo");
        if (modos is null)
            return;

        Verificar(!menu.IsMainPanelShowing, "o painel principal deveria sumir enquanto os modos aparecem");
        Verificar(GetViewport().GuiGetFocusOwner() == modos.HordeButton, "o foco inicial dos modos deveria estar no Horde");

        var cartoes = modos.Cards;
        Verificar(cartoes.Count == 4, $"deveria haver 4 modos na tela; há {cartoes.Count}");
        var habilitados = cartoes.FindAll(c => !c.Disabled);
        Verificar(habilitados.Count == 1 && habilitados[0] == modos.HordeButton, "só o Horde deveria ser jogável");
        foreach (var cartao in cartoes.FindAll(c => c.Disabled))
            Verificar(cartao.Text.Contains("EM BREVE"), $"um modo futuro deveria dizer EM BREVE: \"{cartao.Text}\"");

        Apertar("ui_down");
        await Quadros(2);
        Verificar(GetViewport().GuiGetFocusOwner() == modos.BackButton,
            "seta para baixo do Horde deveria pular os modos desabilitados e cair em VOLTAR");
        Apertar("ui_up");
        await Quadros(2);
        Verificar(GetViewport().GuiGetFocusOwner() == modos.HordeButton, "seta para cima deveria voltar ao Horde");

        Apertar("ui_cancel");
        await AteQue(() => menu.OpenModeMenu is null);
        await Quadros(2);
        Verificar(menu.OpenModeMenu is null, "ESC deveria fechar a escolha de modo (e liberar o nó)");
        Verificar(menu.IsMainPanelShowing, "voltar deveria mostrar o menu principal de novo");
        Verificar(GetViewport().GuiGetFocusOwner() == menu.StartButton, "voltar deveria devolver o foco a INICIAR");

        // --- navegar vinte vezes não acumula nós ---
        await AbrirEFechar(menu);
        var nosBase = NosNaArvore();
        for (var i = 0; i < CiclosDeNavegacao; i++)
            await AbrirEFechar(menu);

        Verificar(NosNaArvore() == nosBase,
            $"vinte idas e voltas não deveriam acumular nós; eram {nosBase}, ficaram {NosNaArvore()}");

        // Enter em VOLTAR na seleção deve voltar, sem começar a partida.
        Verificar(menu.StartButton is not null, "o botão INICIAR deveria existir na volta ao menu");
        menu.StartButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await Quadros(3);
        Verificar(menu.OpenModeMenu?.HordeButton is not null, "HORDE deveria existir na escolha de modo");
        menu.OpenModeMenu?.HordeButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await AteACenaSer("CharacterSelectMenu");
        var selecaoParaVoltar = (CharacterSelectMenu)GetTree().CurrentScene;
        Apertar(InputActionNames.MoveRight);
        await Quadros(2);
        Verificar(selecaoParaVoltar.SelectedIndex == 1, "D deveria escolher o próximo personagem");
        Apertar(InputActionNames.MoveLeft);
        await Quadros(2);
        Verificar(selecaoParaVoltar.SelectedIndex == 0, "A deveria escolher o personagem anterior");
        Verificar(selecaoParaVoltar.BackButton is not null, "VOLTAR deveria existir na seleção");
        selecaoParaVoltar.BackButton?.GrabFocus();
        Apertar("ui_accept");
        await AteACenaSer("MainMenu");
        Verificar(ServiceLocator.Session.SelectedCharacter is null,
            "voltar da seleção não deveria iniciar uma partida");

        // --- menu -> partida -> menu, com a árvore pausada de propósito ---
        var nosDepoisDoCiclo = new List<int>();
        for (var volta = 0; volta < VoltasMenuPartidaMenu; volta++)
        {
            menu = (MainMenu)GetTree().CurrentScene;
            _andamento.Clear();
            _telaDeCarregamentoApareceu = false;

            menu.StartButton!.EmitSignal(BaseButton.SignalName.Pressed);
            await Quadros(3);
            // O bug clássico: o estado de pausa não pode seguir para a próxima tela. Na volta
            // do Enter real a árvore fica solta -- uma UI pausada nem receberia a tecla.
            if (volta != 0)
                GetTree().Paused = true;
            if (volta == 0)
                Apertar("ui_accept"); // Enter no Horde, que nasce com o foco
            else
                menu.OpenModeMenu!.HordeButton!.EmitSignal(BaseButton.SignalName.Pressed);

            Verificar(!GetTree().Paused, "carregar uma partida deveria despausar a árvore");
            await AteACenaSer("CharacterSelectMenu");
            var characterMenu = (CharacterSelectMenu)GetTree().CurrentScene;
            Verificar(characterMenu.CardCount == 2, "o elenco inicial deveria mostrar dois personagens");
            characterMenu.Select(volta % characterMenu.CardCount);
            var chosenId = characterMenu.SelectedDefinition?.Id;
            _andamento.Clear();
            _telaDeCarregamentoApareceu = false;
            Apertar("ui_accept");
            await AteACenaSer("HordeMatch");
            await Quadros(90);

            VerificarCarregamento();
            Verificar(!GetTree().Paused, "a partida deveria nascer despausada");
            Verificar(ServiceLocator.Session.PlayerBody is not null, "a partida deveria ter um jogador");
            Verificar(ServiceLocator.Session.PlayerBody?.Definition?.Id == chosenId,
                "a arena deveria usar o personagem selecionado");
            Verificar(hud is { Visible: true } && ReferenceEquals(hud.BoundPlayer, ServiceLocator.Session.PlayerBody),
                "o HUD deveria estar ligado ao jogador NOVO da partida");

            // Uma volta sai pelo pause: Esc, configurações, voltar, "sair para o menu".
            if (volta == 2)
            {
                await PelaPausa(cenaDoMenu);
                nosDepoisDoCiclo.Add(NosNaArvore());
                continue;
            }

            // A última volta usa o caminho de VERDADE do jogador: morrer, tela de
            // resultado, botão "menu principal". As outras vão direto pelo roteador.
            if (volta == VoltasMenuPartidaMenu - 1)
            {
                await PelaTelaDeResultado();
                nosDepoisDoCiclo.Add(NosNaArvore());
                continue;
            }

            // De volta ao menu, pausado de novo: "sair de uma partida para o menu não deixa o menu pausado".
            _andamento.Clear();
            GetTree().Paused = true;
            ServiceLocator.Router.GoToAsync(MenuPath);
            Verificar(!GetTree().Paused, "voltar ao menu deveria despausar a árvore");
            await AteACenaSer("MainMenu");
            await Quadros(30);

            Verificar(!GetTree().Paused, "o menu não deveria nascer pausado");
            Verificar(ServiceLocator.Session.PlayerBody is null, "de volta ao menu não deveria sobrar jogador registrado");
            Verificar(hud is { Visible: false, BoundPlayer: null }, "de volta ao menu o HUD deveria sumir");
            Verificar(((MainMenu)GetTree().CurrentScene).IsMainPanelShowing, "o menu deveria voltar mostrando o painel principal");

            nosDepoisDoCiclo.Add(NosNaArvore());
        }

        // A primeira volta cria estoque (pool de inimigos); as seguintes não podem crescer.
        Verificar(nosDepoisDoCiclo[^1] == nosDepoisDoCiclo[^2] && nosDepoisDoCiclo[^2] == nosDepoisDoCiclo[^3],
            $"menu -> partida -> menu não deveria acumular nós entre voltas; {string.Join(" -> ", nosDepoisDoCiclo)}");
    }

    /// <summary>O pause de verdade: Esc congela, o HUD fica, configurações abrem e fecham, "sair" volta ao menu despausado.</summary>
    private async Task PelaPausa(string? cenaDeConfiguracoesDoMenu)
    {
        var pausa = Encontrar<PauseMenu>(GetTree().Root);
        Verificar(pausa is { IsOpen: false }, "o menu de pause deveria existir e nascer fechado");
        if (pausa is null)
            return;

        Verificar(pausa.SettingsMenuScene?.ResourcePath == cenaDeConfiguracoesDoMenu,
            "o pause e o menu principal deveriam usar a MESMA cena de configurações");

        Apertar("pause");
        await AteQue(() => pausa.IsOpen);
        Verificar(pausa.IsOpen && GetTree().Paused, "Esc no meio da partida deveria pausar e abrir o menu");
        Verificar(Encontrar<HudController>(GetTree().Root) is { Visible: true }, "o HUD deveria continuar visível atrás do pause");

        Apertar("pause");
        await AteQue(() => !pausa.IsOpen);
        Verificar(!pausa.IsOpen && !GetTree().Paused, "Esc de novo deveria continuar a partida");

        // CONTINUAR pelo botão, e REINICIAR recarrega a partida (cena nova, jogador novo, despausado).
        Apertar("pause");
        await AteQue(() => pausa.IsOpen);
        pausa.ContinueButton!.EmitSignal(BaseButton.SignalName.Pressed);
        await AteQue(() => !pausa.IsOpen);
        Verificar(!pausa.IsOpen && !GetTree().Paused, "o botão CONTINUAR deveria voltar à partida");

        var cenaAntes = GetTree().CurrentScene;
        Apertar("pause");
        await AteQue(() => pausa.IsOpen);
        pausa.RestartButton!.EmitSignal(BaseButton.SignalName.Pressed);
        await AteQue(() => GetTree().CurrentScene != cenaAntes && GetTree().CurrentScene?.Name == "HordeMatch");
        await Quadros(30);
        Verificar(GetTree().CurrentScene != cenaAntes, "REINICIAR deveria recarregar a partida (cena nova)");
        Verificar(!GetTree().Paused && !pausa.IsOpen, "REINICIAR deveria nascer despausado e com o pause fechado");
        Verificar(ServiceLocator.Session.PlayerBody is not null, "a partida reiniciada deveria ter jogador novo");

        Apertar("pause");
        await AteQue(() => pausa.IsOpen);
        pausa.SettingsButton!.EmitSignal(BaseButton.SignalName.Pressed);
        await AteQue(() => pausa.OpenSettings is not null);
        Verificar(pausa.OpenSettings is not null && GetTree().Paused, "CONFIGURAÇÕES no pause deveria abrir os ajustes, ainda pausado");

        Apertar("ui_cancel");
        await AteQue(() => pausa.OpenSettings is null);
        await Quadros(2);
        Verificar(pausa.OpenSettings is null && pausa.IsOpen && GetTree().Paused, "ESC nas configurações deveria voltar ao pause, não à partida");

        pausa.QuitButton!.EmitSignal(BaseButton.SignalName.Pressed);
        Verificar(!GetTree().Paused, "sair do pause para o menu deveria despausar a árvore");
        await AteACenaSer("MainMenu");
        await Quadros(30);

        Verificar(!GetTree().Paused, "o menu não deveria nascer pausado depois de sair do pause");
        Verificar(!pausa.IsOpen, "o pause deveria estar fechado no menu");
        Verificar(ServiceLocator.Session.PlayerBody is null, "de volta ao menu pelo pause não deveria sobrar jogador");
        Verificar(Encontrar<HudController>(GetTree().Root) is { Visible: false }, "o HUD deveria sumir no menu");

        // No menu, Esc não abre o pause.
        Apertar("pause");
        await Quadros(3);
        Verificar(!pausa.IsOpen && !GetTree().Paused, "o pause não deveria abrir no menu principal");
    }

    /// <summary>Morre na partida, espera a tela de resultado e volta ao menu apertando o botão dela.</summary>
    private async Task PelaTelaDeResultado()
    {
        var jogador = ServiceLocator.Session.PlayerBody;
        jogador?.Context?.Health?.ApplyDamage(new Contenda.Components.Health.DamageInfo(
            Amount: 99999f, Type: Contenda.Components.Health.DamageType.Physical, HitPoint: Vector3.Zero,
            Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL, SourceTag: "menu_probe", IsCritical: false));
        await Quadros(30);

        var tela = Encontrar<ResultsScreen>(GetTree().Root);
        Verificar(tela is { IsShowing: true }, "morrer na partida deveria abrir a tela de resultado");
        Verificar(tela?.MainMenuButton is { Disabled: false }, "o botão menu principal deveria estar habilitado agora que o menu existe");

        GetTree().Paused = true;
        tela?.MainMenuButton?.EmitSignal(BaseButton.SignalName.Pressed);
        await AteACenaSer("MainMenu");
        await Quadros(30);

        Verificar(!GetTree().Paused, "do resultado para o menu, a árvore deveria estar despausada");
        Verificar(tela is { IsShowing: false }, "a tela de resultado deveria sumir ao voltar ao menu");
        Verificar(ServiceLocator.Session.PlayerBody is null, "de volta ao menu pelo resultado não deveria sobrar jogador");
        Verificar(Encontrar<HudController>(GetTree().Root) is { Visible: false, BoundPlayer: null }, "o HUD deveria sumir no menu");
    }

    private void VerificarCarregamento()
    {
        Verificar(_andamento.Count >= 2, $"o carregamento deveria anunciar início e fim; anunciou {_andamento.Count} eventos");
        if (_andamento.Count < 2)
            return;

        Verificar(_andamento[0].Loading, "o primeiro aviso deveria dizer que começou a carregar");
        Verificar(_andamento[^1] is { Loading: false, Progress: >= 0.999f }, "o último aviso deveria ser 100% e fim");
        Verificar(_telaDeCarregamentoApareceu, "a tela de carregamento deveria aparecer enquanto carrega");

        for (var i = 1; i < _andamento.Count; i++)
        {
            if (_andamento[i].Progress + 0.0001f < _andamento[i - 1].Progress)
                Verificar(false, "o progresso não deveria andar para trás");
        }
    }

    private async Task AbrirEFechar(MainMenu menu)
    {
        menu.StartButton!.EmitSignal(BaseButton.SignalName.Pressed);
        await AteQue(() => menu.OpenModeMenu is not null);
        Apertar("ui_cancel");
        await AteQue(() => menu.OpenModeMenu is null);
        await Quadros(2);
    }

    private void AoAndarOCarregamento(SceneLoadProgressEvent evento)
    {
        _andamento.Add(evento);

        if (evento.Loading && Encontrar<LoadingScreen>(GetTree().Root) is { IsShowing: true })
            _telaDeCarregamentoApareceu = true;
    }

    private static void Apertar(string acao)
    {
        Godot.Input.ParseInputEvent(new InputEventAction { Action = acao, Pressed = true });
        Godot.Input.ParseInputEvent(new InputEventAction { Action = acao, Pressed = false });
    }

    /// <summary>Espera a condição valer (até ~1 s de jogo): entrada injetada com a árvore pausada nem sempre é entregue no mesmo quadro.</summary>
    private async Task AteQue(Func<bool> condicao)
    {
        for (var i = 0; i < 90 && !condicao(); i++)
            await Quadros(1);
    }

    private async Task Quadros(int quantos)
    {
        for (var i = 0; i < quantos; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task AteACenaSer(string nome)
    {
        for (var i = 0; i < 60 * 30; i++)
        {
            if (GetTree().CurrentScene is { } cena && cena.Name == nome)
                return;

            await Quadros(1);
        }

        Verificar(false, $"a cena \"{nome}\" nunca chegou a ser a corrente");
        throw new TimeoutException($"cena {nome}");
    }

    private int NosNaArvore() => (int)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);

    private bool ExisteJogadorNaArvore()
    {
        foreach (var no in GetTree().GetNodesInGroup(NodeGroups.Player))
        {
            if (GodotObject.IsInstanceValid(no))
                return true;
        }

        return false;
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
        GD.PrintErr($"[menu] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        var perfilDoProbe = ProjectSettings.GlobalizePath("user://probe_menu_profile.cfg");
        if (File.Exists(perfilDoProbe))
            File.Delete(perfilDoProbe);

        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[menu] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[menu] todas as verificações passaram");
        GetTree().Quit();
    }
}
