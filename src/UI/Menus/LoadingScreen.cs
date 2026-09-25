using Contenda.Core;
using Godot;

namespace Contenda.UI.Menus;

/// <summary>A tela de carregamento: cobre a troca de cena com o progresso REAL do carregamento. Ticket 30, spec 11 §1.</summary>
/// <remarks>
/// Vive ao lado do HUD e da tela de resultado (um autoload a adiciona à raiz,
/// fora do nível -- regra 1 do CLAUDE.md) e só obedece a
/// <see cref="GameEvents.SceneLoadProgress"/>: quem sabe quanto falta é o
/// <see cref="SceneRouter"/>, esta tela não inventa progresso.
/// </remarks>
public sealed partial class LoadingScreen : CanvasLayer
{
    [Export] public NodePath PanelPath { get; set; } = new();

    [Export] public NodePath BarPath { get; set; } = new();

    private Control? _painel;
    private ProgressBar? _barra;

    /// <summary>Se a tela está visível agora. Para o probe/depuração.</summary>
    public bool IsShowing => _painel?.Visible ?? false;

    public override void _Ready()
    {
        _painel = GetNodeOrNull<Control>(PanelPath);
        _barra = GetNodeOrNull<ProgressBar>(BarPath);

        if (_painel is null || _barra is null)
        {
            GD.PushError($"{Name}: PanelPath ou BarPath não resolveram.");
            return;
        }

        _painel.Visible = false;
        ServiceLocator.Events.SceneLoadProgress += AoAndarOCarregamento;
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.SceneLoadProgress -= AoAndarOCarregamento;
    }

    private void AoAndarOCarregamento(SceneLoadProgressEvent evento)
    {
        if (_painel is null || _barra is null)
            return;

        _painel.Visible = evento.Loading;
        _barra.Value = evento.Progress * _barra.MaxValue;
    }
}
