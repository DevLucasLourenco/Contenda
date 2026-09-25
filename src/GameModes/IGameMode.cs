using System;
using Godot;

namespace Contenda.GameModes;

/// <summary>
/// As regras de uma partida: quando começa, o que a vence, o que a perde.
/// </summary>
/// <remarks>
/// O modo é um nó filho da arena; a arena, a câmera e o personagem não sabem
/// qual modo está ativo. Ver docs/specs/10-modos-de-jogo-horde.md §1.
///
/// Deliberadamente sem <c>PauseMatch</c> (também na spec): pausar é o ticket
/// 32 (pause e configurações), e uma interface com um método que nenhum modo
/// implementa de verdade ainda só declara uma promessa. Entra junto com o
/// menu de pause. O mesmo vale para `Paused`/`Loading` do estado: só existem
/// em <see cref="GameModeState"/> os que algum modo já percorre.
/// </remarks>
public interface IGameMode
{
    StringName Id { get; }

    GameModeState State { get; }

    void Initialize(GameModeConfig config);

    void StartMatch();

    void EndMatch(GameModeResult result);

    event Action<GameModeState>? StateChanged;

    event Action<GameModeResult>? MatchEnded;
}
