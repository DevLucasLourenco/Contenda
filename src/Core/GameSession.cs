using Godot;

namespace Contenda.Core;

/// <summary>
/// Estado da partida corrente: o que o jogador escolheu no menu e o que sobrou
/// da última partida.
/// </summary>
/// <remarks>
/// Nada aqui é persistido em disco. Configurações vão para
/// <c>user://settings.cfg</c> e recordes para <c>user://profile.cfg</c>, ambos
/// no M7 — ver docs/specs/14-configuracoes-persistencia-e-build.md.
///
/// Os campos de modo, personagem e resultado entram junto com as telas que os
/// preenchem (M6 e M7).
/// </remarks>
public sealed partial class GameSession : Node
{
    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GD.Print("[boot] GameSession pronto");
    }

    /// <summary>Descarta o estado da partida ao voltar para o menu.</summary>
    public void ResetToMenu()
    {
    }
}
