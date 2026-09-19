namespace Contenda.GameModes.Horde;

/// <summary>
/// Decide quando uma onda está limpa e quando o respiro seguinte termina,
/// sem nó nenhum.
/// </summary>
/// <remarks>
/// POCO de propósito, mesma disciplina de <c>EnemyStateMachine</c>: as
/// janelas de tempo (o alçapão de inimigo preso, o respiro entre ondas) são
/// onde um erro de comparação passa despercebido, e só dá para testar isso
/// controlando o relógio fora da engine. Ver docs/specs/15-qualidade-testes-e-performance.md
/// §1-2 ("WaveDirector avança com relógio simulado; fallback do inimigo
/// preso" -- xUnit) e docs/specs/10-modos-de-jogo-horde.md §4.
///
/// Não sabe nada de spawn, pool nem definição de onda -- só recebe os dois
/// sinais já resolvidos (<c>enemiesRemaining</c>, <c>realActiveCount</c>) a
/// cada quadro. Quem produz esses sinais é o <c>WaveDirector</c>, na
/// fronteira com a engine.
/// </remarks>
public sealed class WaveClearTimer
{
    private readonly float _completionDelay;
    private readonly float _stuckFallbackSeconds;

    private float _relogioDeTravado;
    private float _relogioDeRespiro;

    public WaveClearTimer(float completionDelay, float stuckFallbackSeconds)
    {
        _completionDelay = completionDelay;
        _stuckFallbackSeconds = stuckFallbackSeconds;
    }

    /// <summary>
    /// Se a última vez que <see cref="TickWaitingForClear"/> devolveu
    /// <c>true</c> foi pelo alçapão (discordância que passou de
    /// <see cref="_stuckFallbackSeconds"/>), não pelo caminho normal (pool
    /// zerado). Para quem chama decidir se avisa no log.
    /// </summary>
    public bool StuckFallbackTriggered { get; private set; }

    /// <summary>
    /// Avança o relógio de espera pela limpeza da onda.
    /// </summary>
    /// <remarks>
    /// <paramref name="enemiesRemaining"/> == 0 sozinho não significa
    /// "ninguém mais no mundo": um abate só libera o pool depois da
    /// "animação" de morte (<c>DeathDuration</c>), então
    /// <paramref name="realActiveCount"/> ainda mostrar alguém logo após o
    /// último abate é NORMAL, não travamento -- o alçapão só acumula
    /// ENQUANTO essa discordância persistir, e o caminho comum (o pool zera
    /// antes do alçapão vencer) nunca o aciona.
    ///
    /// Escopo deliberadamente estreito: isto cobre só a discordância de
    /// CONTAGEM (um <c>EnemyKilled</c> que não chegou até aqui -- bug), não
    /// um inimigo genuinamente vivo e inalcançável (<paramref
    /// name="enemiesRemaining"/> continua maior que zero enquanto ele não
    /// morre, e o relógio de travado nem começa a contar nesse caso -- de
    /// propósito, senão qualquer inimigo vivo forçaria o avanço da onda). O
    /// caso "vivo, preso atrás de um obstáculo" é responsabilidade do
    /// sistema de navegação do ticket 23, não deste relógio. Ver a seção
    /// "Alçapão de inimigo preso" do ticket 27 para a leitura completa.
    /// </remarks>
    /// <param name="delta">Tempo do quadro, em segundos.</param>
    /// <param name="enemiesRemaining">Planejados mais ativos, menos abatidos -- o bookkeeping do dono.</param>
    /// <param name="realActiveCount">Quantos o pool de verdade ainda reporta ativos.</param>
    /// <returns>Verdadeiro no quadro em que a onda deveria ser considerada limpa.</returns>
    public bool TickWaitingForClear(float delta, int enemiesRemaining, int realActiveCount)
    {
        if (enemiesRemaining > 0)
        {
            _relogioDeTravado = 0f;
            return false;
        }

        if (realActiveCount <= 0)
        {
            StuckFallbackTriggered = false;
            return true;
        }

        _relogioDeTravado += delta;
        if (_relogioDeTravado < _stuckFallbackSeconds)
            return false;

        StuckFallbackTriggered = true;
        return true;
    }

    /// <summary>Avança o relógio de respiro entre ondas.</summary>
    /// <returns>Verdadeiro no quadro em que o respiro termina.</returns>
    public bool TickResting(float delta)
    {
        _relogioDeRespiro += delta;
        return _relogioDeRespiro >= _completionDelay;
    }
}
