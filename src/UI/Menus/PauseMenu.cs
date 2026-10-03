using Contenda.Core;
using Godot;

namespace Contenda.UI.Menus;

/// <summary>
/// O pause: Esc no meio da partida congela tudo e abre continuar,
/// configurações, reiniciar e sair para o menu. Spec 11 §3.5, ticket 32.
/// </summary>
/// <remarks>
/// Vive ao lado do HUD (um autoload a adiciona à raiz) e roda com a árvore
/// pausada (<c>ProcessMode = Always</c>). O HUD continua visível atrás -- o
/// jogador pausa exatamente para ler as combinações --, e o "blur" da spec não
/// foi feito (fica para a passada visual).
///
/// Só abre com uma partida acontecendo: há um jogador vivo e nenhum resultado
/// pendente (o resultado é a tela dele, não do pause). As transições passam
/// pelo <see cref="SceneRouter"/>, que despausa a árvore -- é o que impede o
/// menu de nascer pausado.
/// </remarks>
public sealed partial class PauseMenu : CanvasLayer
{
    [Export] public NodePath PanelPath { get; set; } = new();

    [Export] public NodePath ContinueButtonPath { get; set; } = new();

    [Export] public NodePath SettingsButtonPath { get; set; } = new();

    [Export] public NodePath RestartButtonPath { get; set; } = new();

    [Export] public NodePath QuitButtonPath { get; set; } = new();

    [Export] public PackedScene? SettingsMenuScene { get; set; }

    [Export(PropertyHint.File, "*.tscn")]
    public string MainMenuScenePath { get; set; } = "res://scenes/ui/menus/MainMenu.tscn";

    private Control? _painel;
    private Button? _continuar;
    private Button? _configuracoes;
    private Button? _reiniciar;
    private Button? _sair;
    private SettingsMenu? _ajustes;

    public bool IsOpen => _painel?.Visible ?? false;

    public Button? ContinueButton => _continuar;

    public Button? SettingsButton => _configuracoes;

    public Button? RestartButton => _reiniciar;

    public Button? QuitButton => _sair;

    public SettingsMenu? OpenSettings => _ajustes;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        _painel = GetNodeOrNull<Control>(PanelPath);
        _continuar = GetNodeOrNull<Button>(ContinueButtonPath);
        _configuracoes = GetNodeOrNull<Button>(SettingsButtonPath);
        _reiniciar = GetNodeOrNull<Button>(RestartButtonPath);
        _sair = GetNodeOrNull<Button>(QuitButtonPath);

        if (_painel is null || _continuar is null || _configuracoes is null || _reiniciar is null || _sair is null
            || SettingsMenuScene is null)
        {
            GD.PushError($"{Name}: algum caminho de nó ou a cena de configurações não resolveu.");
            return;
        }

        _painel.Visible = false;

        _continuar.Pressed += () =>
        {
            ServiceLocator.Audio.PlayUi(AudioDirector.CueIds.UiBack);
            Fechar();
        };
        _configuracoes.Pressed += () =>
        {
            ServiceLocator.Audio.PlayUi(AudioDirector.CueIds.UiClick);
            AbrirConfiguracoes();
        };
        _reiniciar.Pressed += () =>
        {
            ServiceLocator.Audio.PlayUi(AudioDirector.CueIds.UiConfirm);
            Fechar();
            ServiceLocator.Router.Reload();
        };
        _sair.Pressed += () =>
        {
            ServiceLocator.Audio.PlayUi(AudioDirector.CueIds.UiBack);
            Fechar();
            ServiceLocator.Router.GoToAsync(MainMenuScenePath);
        };
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (!evento.IsActionPressed(InputActions.Pause))
            return;

        if (IsOpen)
        {
            // Com as configurações abertas, o mesmo Esc fecha só elas (o SettingsMenu já tratou ui_cancel).
            if (_ajustes is null)
            {
                GetViewport().SetInputAsHandled();
                Fechar();
            }

            return;
        }

        if (!HaPartidaAcontecendo())
            return;

        GetViewport().SetInputAsHandled();
        Abrir();
    }

    public void Abrir()
    {
        if (_painel is null || _continuar is null)
            return;

        GetTree().Paused = true;
        _painel.Visible = true;
        _continuar.GrabFocus();
    }

    public void Fechar()
    {
        FecharConfiguracoes();

        if (_painel is not null)
            _painel.Visible = false;

        GetTree().Paused = false;
    }

    private void AbrirConfiguracoes()
    {
        if (_ajustes is not null || _painel is null || SettingsMenuScene is null)
            return;

        _ajustes = SettingsMenuScene.Instantiate<SettingsMenu>();
        AddChild(_ajustes);
        _ajustes.BackRequested += FecharConfiguracoes;
        _painel.Visible = false;
    }

    private void FecharConfiguracoes()
    {
        if (_ajustes is null)
            return;

        _ajustes.QueueFree();
        _ajustes = null;

        if (_painel is not null && GetTree().Paused)
        {
            _painel.Visible = true;
            _configuracoes?.GrabFocus();
        }
    }

    /// <summary>Há um jogador vivo, sem resultado de partida pendente, e nada carregando.</summary>
    private static bool HaPartidaAcontecendo()
    {
        var sessao = ServiceLocator.Session;
        return sessao.PlayerBody is { } jogador
            && GodotObject.IsInstanceValid(jogador)
            && jogador.Context?.Health is { IsAlive: true }
            && sessao.LastResult is null;
    }
}
