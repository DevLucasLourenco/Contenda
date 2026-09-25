using Contenda.Core;
using Godot;

namespace Contenda.UI.Menus;

/// <summary>
/// O menu principal: INICIAR, CONFIGURAÇÕES, SAIR, e a escolha de modo por cima.
/// Spec 11 §3.1-3.2, ticket 30.
/// </summary>
/// <remarks>
/// A escolha de modo é um overlay <see cref="GameModeMenu"/> instanciado e
/// LIBERADO a cada visita -- nenhuma tela fica esquecida na árvore ("navegar
/// vinte vezes não acumula nós esquecidos"). O caminho até a partida passa pelo
/// <see cref="SceneRouter.GoToAsync"/>, então a tela de carregamento mostra
/// progresso de verdade e a árvore nunca chega despausada ao jogo.
///
/// CONFIGURAÇÕES aparece, desabilitado, até a tela de configurações existir
/// (ticket 32) -- mesma regra do "EM BREVE" dos modos futuros.
/// </remarks>
public sealed partial class MainMenu : Node
{
    [Export] public NodePath MainPanelPath { get; set; } = new();

    [Export] public NodePath StartButtonPath { get; set; } = new();

    [Export] public NodePath SettingsButtonPath { get; set; } = new();

    [Export] public NodePath QuitButtonPath { get; set; } = new();

    /// <summary>Onde o overlay de modo entra (a camada de interface).</summary>
    [Export] public NodePath OverlayRootPath { get; set; } = new();

    [Export] public NodePath BackdropPath { get; set; } = new();

    [Export] public PackedScene? ModeMenuScene { get; set; }

    /// <summary>A partida do modo horda.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string HordeScenePath { get; set; } = "res://scenes/arena/HordeMatch.tscn";

    [Export(PropertyHint.File, "*.tscn")]
    public string SettingsScenePath { get; set; } = "res://scenes/ui/menus/SettingsMenu.tscn";

    private Control? _painel;
    private Button? _iniciar;
    private Button? _configuracoes;
    private Button? _sair;
    private Node? _overlayRoot;
    private GameModeMenu? _modos;

    /// <summary>O fundo 3D. Para o probe/depuração.</summary>
    public MenuBackdrop? Backdrop => GetNodeOrNull<MenuBackdrop>(BackdropPath);

    public Button? StartButton => _iniciar;

    public Button? SettingsButton => _configuracoes;

    public Button? QuitButton => _sair;

    /// <summary>O overlay de escolha de modo, se aberto agora. Para o probe/depuração.</summary>
    public GameModeMenu? OpenModeMenu => _modos;

    public bool IsMainPanelShowing => _painel?.Visible ?? false;

    public override void _Ready()
    {
        _painel = GetNodeOrNull<Control>(MainPanelPath);
        _iniciar = GetNodeOrNull<Button>(StartButtonPath);
        _configuracoes = GetNodeOrNull<Button>(SettingsButtonPath);
        _sair = GetNodeOrNull<Button>(QuitButtonPath);
        _overlayRoot = GetNodeOrNull<Node>(OverlayRootPath);

        if (_painel is null || _iniciar is null || _configuracoes is null || _sair is null
            || _overlayRoot is null || ModeMenuScene is null)
        {
            GD.PushError($"{Name}: algum caminho de nó ou a cena do menu de modos não resolveu.");
            return;
        }

        // Voltar ao menu descarta o estado da partida anterior (spec 14 §4).
        ServiceLocator.Session.ResetToMenu();

        _iniciar.Pressed += AbrirModos;
        _sair.Pressed += () => GetTree().Quit();

        // Visível e desabilitado até a tela de destino existir.
        if (!ResourceLoader.Exists(SettingsScenePath))
            ComingSoon.Apply(_configuracoes);

        _iniciar.GrabFocus();
    }

    private void AbrirModos()
    {
        if (_modos is not null || _painel is null || _overlayRoot is null || ModeMenuScene is null)
            return;

        _modos = ModeMenuScene.Instantiate<GameModeMenu>();
        _overlayRoot.AddChild(_modos);
        _painel.Visible = false;

        _modos.ModeChosen += AoEscolherModo;
        _modos.BackRequested += FecharModos;
    }

    private void FecharModos()
    {
        if (_modos is null)
            return;

        _modos.QueueFree();
        _modos = null;

        if (_painel is not null)
            _painel.Visible = true;

        _iniciar?.GrabFocus();
    }

    private void AoEscolherModo(string idDoModo)
    {
        if (idDoModo == GameModeMenu.HordeModeId)
            ServiceLocator.Router.GoToAsync(HordeScenePath);
    }
}
