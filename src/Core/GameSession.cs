using Contenda.Characters.Base;
using Contenda.GameModes;
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
    /// <summary>
    /// O personagem do jogador na cena atual. Nulo fora de uma partida.
    /// </summary>
    /// <remarks>
    /// Quem se registra é o próprio <c>CharacterController</c> com
    /// <c>Team.Player</c>, no próprio <c>_Ready</c> -- nós não procuram o
    /// jogador, ele se anuncia, mesmo espírito do <see cref="ServiceLocator"/>.
    /// Existe para o ticket 22: um <c>EnemyBrain</c> que precisasse escanear
    /// <c>GetNodesInGroup</c> a cada quadro de física alocaria por quadro,
    /// proibido pelas convenções §5 -- uma referência cacheada não.
    /// </remarks>
    public CharacterController? PlayerBody { get; set; }

    /// <summary>
    /// O chefe da onda atual, se houver um. Nulo na maior parte da partida.
    /// </summary>
    /// <remarks>
    /// Mesmo espírito de <see cref="PlayerBody"/>, mas quem se anuncia é o
    /// próprio <c>EnemyBrain</c> quando a <c>EnemyDefinition</c> marca
    /// <c>IsBoss</c> (ticket 26) -- ao <c>Configure</c> (nascimento) e a
    /// cada <c>ResetForSpawn</c> (reciclagem do pool, já que só o primeiro
    /// roda de novo, nunca o segundo). Ao contrário do jogador, que vive a
    /// partida inteira, um chefe pode morrer -- <c>EnemyBrain.AoMorrer</c>
    /// zera este campo, e o <c>HudController</c> reage escondendo a própria
    /// barra.
    /// </remarks>
    public CharacterController? BossBody { get; set; }

    /// <summary>
    /// O resultado da última partida terminada. Nulo antes da primeira. A tela
    /// de resultado (ticket 29) lê daqui. Spec 10 §9.
    /// </summary>
    public GameModeResult? LastResult { get; set; }

    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GD.Print("[boot] GameSession pronto");
    }

    /// <summary>Descarta o estado da partida ao voltar para o menu.</summary>
    public void ResetToMenu()
    {
        PlayerBody = null;
        BossBody = null;
        LastResult = null;
    }
}
