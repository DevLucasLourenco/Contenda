using Godot;

namespace Contenda.GameModes;

/// <summary>O que sobra de uma partida, para a tela de resultado e o recorde. Spec 10 §1, §9.</summary>
/// <param name="Victory">Se o jogador venceu.</param>
/// <param name="Score">
/// Pontuação. Sempre zero por enquanto: o cálculo (spec 10 §8) é o ticket 29 --
/// o campo existe porque o contrato da spec já o declara e a tela de
/// resultado vai lê-lo, não para ser preenchido antes de existir a regra.
/// </param>
/// <param name="WavesCleared">Quantas ondas o jogador limpou por inteiro.</param>
/// <param name="EnemiesKilled">Abates na partida, contados pelo evento de abate.</param>
/// <param name="DurationSeconds">Tempo de jogo, em segundos.</param>
/// <param name="CharacterId">Quem o jogador era.</param>
public readonly record struct GameModeResult(
    bool Victory,
    int Score,
    int WavesCleared,
    int EnemiesKilled,
    float DurationSeconds,
    StringName CharacterId);
