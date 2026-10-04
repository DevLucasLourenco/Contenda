using System;
using Godot;

namespace Contenda.Core;

/// <summary>
/// Troca de cena.
/// </summary>
/// <remarks>
/// A regra que este nó existe para centralizar: **toda transição despausa a
/// árvore**. Sem isso, sair do pause para o menu faz o menu nascer pausado — um
/// dos bugs previsíveis listados em
/// docs/specs/15-qualidade-testes-e-performance.md §5.
///
/// <see cref="GoToAsync"/> carrega em segundo plano
/// (<c>ResourceLoader.LoadThreadedRequest</c>) e anuncia o progresso REAL por
/// <see cref="GameEvents.SceneLoadProgress"/> -- a tela de carregamento só
/// desenha o que este nó relata (ticket 30, spec 11 §1).
/// </remarks>
public sealed partial class SceneRouter : Node
{
    private string? _carregando;
    private readonly Godot.Collections.Array _progresso = [];

    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GameLog.Debug("[boot] SceneRouter pronto");
    }

    /// <summary>Troca a cena corrente na hora, garantindo que a árvore não fique pausada.</summary>
    public void GoTo(string scenePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(scenePath);

        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(scenePath);
    }

    /// <summary>
    /// Carrega <paramref name="scenePath"/> em segundo plano, mostrando o
    /// progresso, e só então troca. A árvore é despausada já -- uma tela de
    /// carregamento nunca nasce congelada.
    /// </summary>
    public void GoToAsync(string scenePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(scenePath);

        if (_carregando is not null)
            return;

        GetTree().Paused = false;

        if (ResourceLoader.LoadThreadedRequest(scenePath) != Error.Ok)
        {
            GD.PushError($"{Name}: não consegui começar a carregar {scenePath}.");
            return;
        }

        _carregando = scenePath;
        ServiceLocator.Events.RaiseSceneLoadProgress(new SceneLoadProgressEvent(0f, Loading: true));
    }

    /// <remarks>
    /// `_PhysicsProcess`, não `_Process`: convenções §4, e é o que deixa o
    /// probe headless verificar. Sem processo enquanto nada carrega.
    /// </remarks>
    public override void _PhysicsProcess(double delta)
    {
        if (_carregando is not { } caminho)
            return;

        var estado = ResourceLoader.LoadThreadedGetStatus(caminho, _progresso);

        switch (estado)
        {
            case ResourceLoader.ThreadLoadStatus.InProgress:
                var andamento = _progresso.Count > 0 ? (float)_progresso[0].AsDouble() : 0f;
                ServiceLocator.Events.RaiseSceneLoadProgress(new SceneLoadProgressEvent(andamento, Loading: true));
                break;

            case ResourceLoader.ThreadLoadStatus.Loaded:
                Concluir(caminho);
                break;

            default:
                GD.PushError($"{Name}: o carregamento de {caminho} falhou ({estado}).");
                _carregando = null;
                ServiceLocator.Events.RaiseSceneLoadProgress(new SceneLoadProgressEvent(0f, Loading: false));
                break;
        }
    }

    private void Concluir(string caminho)
    {
        _carregando = null;

        var cena = (PackedScene)ResourceLoader.LoadThreadedGet(caminho);
        ServiceLocator.Events.RaiseSceneLoadProgress(new SceneLoadProgressEvent(1f, Loading: false));
        GetTree().ChangeSceneToPacked(cena);
    }

    /// <summary>Recarrega a cena corrente. Usado pelo "tentar novamente".</summary>
    public void Reload()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }
}
