using System;
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

        // --- iniciar leva à escolha de modo; teclado; voltar ---
        // Enter no botão que tem o foco, não um sinal: prova foco + ação juntos.
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
        await Quadros(5);
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
            await AteACenaSer("HordeMatch");
            await Quadros(90);

            VerificarCarregamento();
            Verificar(!GetTree().Paused, "a partida deveria nascer despausada");
            Verificar(ServiceLocator.Session.PlayerBody is not null, "a partida deveria ter um jogador");
            Verificar(hud is { Visible: true } && ReferenceEquals(hud.BoundPlayer, ServiceLocator.Session.PlayerBody),
                "o HUD deveria estar ligado ao jogador NOVO da partida");

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
        await Quadros(3);
        Apertar("ui_cancel");
        await Quadros(5);
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
