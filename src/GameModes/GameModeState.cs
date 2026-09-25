namespace Contenda.GameModes;

/// <summary>Em que fase da vida uma partida está. Ver <see cref="IGameMode"/> para o que ficou de fora e por quê.</summary>
public enum GameModeState
{
    /// <summary>Criado, mas <see cref="IGameMode.StartMatch"/> ainda não foi chamado (ou está esperando o estoque de inimigos ficar pronto).</summary>
    Starting,

    /// <summary>A partida está rolando.</summary>
    Playing,

    /// <summary>Acabou -- vitória ou derrota. Só sai daqui recomeçando a arena.</summary>
    Ended,
}
