using System;
using Contenda.Components.AI;
using Contenda.Core;
using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>
/// O laço central do modo horda: aparece uma onda, o jogador limpa, respira,
/// a próxima começa. Ticket 27, spec 10 §4.
/// </summary>
/// <remarks>
/// Não sabe COMO um inimigo nasce (isso é o <see cref="SpawnDirector"/>) nem
/// COMO o jogo termina (vitória/derrota são o <c>HordeGameMode</c> do ticket
/// 28, que ainda não existe) -- só orquestra a sequência de
/// <see cref="WaveDefinition"/> de um <see cref="WaveSetDefinition"/>.
///
/// A decisão de QUANDO uma onda está limpa (e o alçapão de inimigo preso)
/// mora em <see cref="WaveClearTimer"/>, e QUEM nasce a seguir entre
/// entradas concorrentes mora em <see cref="SpawnQueue"/> -- ambos POCOs
/// testados em xUnit (spec 15 §1-2: "WaveDirector avança com relógio
/// simulado; fallback do inimigo preso" é literalmente um requisito de
/// teste da spec). Este nó só produz os sinais que os dois consomem
/// (contagem de abates, `EnemyPool.ActiveCount`) e traduz o resultado de
/// volta em ações de engine (pedir um spawn, disparar eventos) -- a mesma
/// fronteira que <c>EnemyBrain</c> já mantém para <c>EnemyStateMachine</c>.
///
/// Contagem de abates por <see cref="GameEvents.EnemyKilled"/>, nunca por
/// varredura de cena a cada quadro (spec 10 §4) -- <see cref="EnemiesRemaining"/>
/// é bookkeeping PRÓPRIO (planejados menos abatidos), cruzado contra
/// `EnemyPool.ActiveCount` (a verdade do pool) só para o alçapão.
/// </remarks>
public sealed partial class WaveDirector : Node
{
    /// <summary>O <see cref="SpawnDirector"/> que materializa cada spawn pedido.</summary>
    [Export] public NodePath SpawnDirectorPath { get; set; } = new();

    /// <summary>
    /// Quanto tempo <see cref="EnemiesRemaining"/> pode ficar em zero com o
    /// pool ainda reportando alguém ativo antes de forçar a próxima onda.
    /// </summary>
    /// <remarks>
    /// "Um inimigo preso no cenário não trava a partida" -- ticket 27. Rede
    /// de segurança, não o caminho normal: um abate perdido (bug) ou um
    /// inimigo genuinamente preso atrás de um obstáculo são a MESMA coisa do
    /// ponto de vista de quem está jogando, e os dois precisam do mesmo
    /// alçapão.
    /// </remarks>
    [Export(PropertyHint.Range, "1,30,0.5")] public float StuckFallbackSeconds { get; set; } = 5f;

    /// <summary>
    /// Teto DURO de inimigos ativos ao mesmo tempo, através de qualquer
    /// onda -- spec 10 §7: "Inimigos ativos simultâneos (teto duro): 40".
    /// </summary>
    /// <remarks>
    /// Separado de <c>WaveDefinition.MaxConcurrent</c> (o teto DESTA onda,
    /// dado de balanceamento por-onda) de propósito: `MaxConcurrent` é só um
    /// hint de intervalo no editor (`[Export(PropertyHint.Range, "1,40,1")]`),
    /// não uma trava de verdade -- nada impede um `.tres` de pedir mais que
    /// 40. O menor dos dois é quem realmente vale a cada quadro.
    /// </remarks>
    [Export(PropertyHint.Range, "1,60,1")] public int GlobalActiveCap { get; set; } = 40;

    private enum Fase { Ocioso, Spawnando, EsperandoLimpeza, Respiro, Concluido }

    private SpawnDirector? _spawnDirector;
    private EnemyPool? _pool;

    private WaveSetDefinition? _conjunto;
    private Fase _fase = Fase.Ocioso;

    private SpawnQueue? _fila;
    private WaveClearTimer? _relogio;
    private float _relogioDeSpawn;

    private int _totalDaOnda;
    private int _abatidosNaOnda;

    /// <summary>Índice da onda atual, 0-based. -1 antes de começar.</summary>
    public int CurrentWaveIndex { get; private set; } = -1;

    /// <summary>A onda em andamento agora. Nulo antes de <see cref="Begin"/> ou depois da última.</summary>
    public WaveDefinition? CurrentWave { get; private set; }

    /// <summary>Planejados (ainda por spawnar) mais ativos, menos os já abatidos. Ver spec 10 §4.</summary>
    public int EnemiesRemaining => Mathf.Max(0, _totalDaOnda - _abatidosNaOnda);

    /// <summary>Dispara ao começar cada onda, com a própria onda e o índice. Para o banner e o probe.</summary>
    public event Action<WaveDefinition, int>? WaveStarted;

    /// <summary>Dispara ao limpar cada onda (ou o alçapão de inimigo preso forçar o avanço).</summary>
    public event Action<WaveDefinition, int>? WaveCleared;

    /// <summary>Dispara quando a última onda do conjunto termina.</summary>
    public event Action? AllWavesCleared;

    public override void _Ready()
    {
        _spawnDirector = GetNodeOrNull<SpawnDirector>(SpawnDirectorPath);
        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");

        if (_spawnDirector is null || _pool is null)
            GD.PushError($"{Name}: SpawnDirectorPath ou EnemyPool não resolveram.");

        ServiceLocator.Events.EnemyKilled += AoMatarInimigo;
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.EnemyKilled -= AoMatarInimigo;
    }

    /// <summary>Começa a rodar um conjunto de ondas, do início.</summary>
    public void Begin(WaveSetDefinition conjunto)
    {
        ArgumentNullException.ThrowIfNull(conjunto);

        _conjunto = conjunto;
        CurrentWaveIndex = -1;
        IniciarProximaOnda();
    }

    /// <summary>Para tudo -- nenhum spawn pendente continua, nenhum evento mais dispara. Debug/fim de partida.</summary>
    public void Abort()
    {
        _fase = Fase.Concluido;
        CurrentWave = null;
        _fila = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (_fase)
        {
            case Fase.Spawnando: TickSpawnando((float)delta); break;
            case Fase.EsperandoLimpeza: TickEsperandoLimpeza((float)delta); break;
            case Fase.Respiro: TickRespiro((float)delta); break;
        }
    }

    private void IniciarProximaOnda()
    {
        if (_conjunto is null || CurrentWaveIndex + 1 >= _conjunto.Waves.Length)
        {
            _fase = Fase.Concluido;
            CurrentWave = null;
            AllWavesCleared?.Invoke();
            return;
        }

        CurrentWaveIndex++;
        var onda = _conjunto.Waves[CurrentWaveIndex];
        CurrentWave = onda;

        var entradas = new (int Count, float Weight, float DelayBeforeFirst)[onda.Entries.Length];
        _totalDaOnda = 0;

        for (var i = 0; i < onda.Entries.Length; i++)
        {
            var entrada = onda.Entries[i];
            var contagem = entrada.Enemy is null ? 0 : Mathf.Max(0, entrada.Count);
            entradas[i] = (contagem, entrada.Weight, entrada.DelayBeforeFirst);
            _totalDaOnda += contagem;
        }

        _fila = new SpawnQueue(entradas);
        _relogio = new WaveClearTimer(onda.CompletionDelay, StuckFallbackSeconds);
        _relogioDeSpawn = 0f;
        _abatidosNaOnda = 0;

        _fase = _totalDaOnda > 0 ? Fase.Spawnando : Fase.EsperandoLimpeza;

        ServiceLocator.Events.RaiseWaveAnnounced(new WaveAnnouncedEvent(onda.DisplayName));
        WaveStarted?.Invoke(onda, CurrentWaveIndex);
    }

    private void TickSpawnando(float delta)
    {
        // `CurrentWave`/`_fila` sempre não-nulos aqui: só entram em
        // Fase.Spawnando dentro de IniciarProximaOnda, que os monta os dois
        // juntos antes de trocar a fase.
        if (CurrentWave is not { } onda || _fila is not { } fila)
            return;

        fila.Tick(delta);

        if (fila.IsEmpty)
        {
            _fase = Fase.EsperandoLimpeza;
            return;
        }

        var tetoDeVerdade = Mathf.Min(onda.MaxConcurrent, GlobalActiveCap);
        var ativos = _pool?.ActiveCount ?? 0;
        if (ativos >= tetoDeVerdade)
            return;

        _relogioDeSpawn += delta;
        if (_relogioDeSpawn < onda.SpawnInterval)
            return;

        var indice = fila.ChooseNext(GD.Randf());
        if (indice < 0)
            return; // ninguém elegível ainda (todos em DelayBeforeFirst) -- tenta de novo no próximo quadro

        _relogioDeSpawn = 0f;
        fila.Consume(indice);

        var definicao = onda.Entries[indice].Enemy;
        if (definicao is not null)
            _spawnDirector?.RequestSpawn(definicao);

        if (fila.IsEmpty)
            _fase = Fase.EsperandoLimpeza;
    }

    private void TickEsperandoLimpeza(float delta)
    {
        if (CurrentWave is not { } onda || _relogio is not { } relogio)
            return;

        var ativos = _pool?.ActiveCount ?? 0;
        if (!relogio.TickWaitingForClear(delta, EnemiesRemaining, ativos))
            return;

        if (relogio.StuckFallbackTriggered)
        {
            GD.PushWarning(
                $"{Name}: {ativos} inimigo(s) ainda ativo(s) {StuckFallbackSeconds:0}s depois de " +
                $"\"{onda.DisplayName}\" contar zero restantes -- forçando avanço de onda.");
        }

        _fase = Fase.Respiro;
        WaveCleared?.Invoke(onda, CurrentWaveIndex);
    }

    private void TickRespiro(float delta)
    {
        if (_relogio is not { } relogio)
            return;

        if (relogio.TickResting(delta))
            IniciarProximaOnda();
    }

    private void AoMatarInimigo(EnemyKilledEvent evento)
    {
        if (_fase is Fase.Spawnando or Fase.EsperandoLimpeza)
            _abatidosNaOnda++;
    }
}
