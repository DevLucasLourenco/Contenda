using System;
using Godot;

namespace Contenda.Core;

/// <summary>
/// Troca de cena.
/// </summary>
/// <remarks>
/// O carregamento assíncrono com tela de progresso é do M7. O que já vale hoje é
/// a regra que este nó existe para centralizar: **toda transição despausa a
/// árvore**. Sem isso, sair do pause para o menu faz o menu nascer pausado — um
/// dos bugs previsíveis listados em
/// docs/specs/15-qualidade-testes-e-performance.md §5.
/// </remarks>
public sealed partial class SceneRouter : Node
{
    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GD.Print("[boot] SceneRouter pronto");
    }

    /// <summary>Troca a cena corrente, garantindo que a árvore não fique pausada.</summary>
    public void GoTo(string scenePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(scenePath);

        GetTree().Paused = false;
        GetTree().ChangeSceneToFile(scenePath);
    }

    /// <summary>Recarrega a cena corrente. Usado pelo "tentar novamente".</summary>
    public void Reload()
    {
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }
}
