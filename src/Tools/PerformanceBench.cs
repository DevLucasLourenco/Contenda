using System;
using System.Globalization;
using Contenda.Camera;
using Contenda.Characters.Base;
using Contenda.Components.Abilities;
using Contenda.Components.AI;
using Contenda.Core;
using Godot;

namespace Contenda.Tools;

/// <summary>Benchmark repetível da arena urbana com quarenta inimigos e VFX.</summary>
/// <remarks>
/// Execute com <c>godot --path . -- --bench=40enemies</c>. O padrão mede 60 s
/// depois de 5 s de aquecimento; para um smoke test, use
/// <c>--bench-seconds=5</c>. O CSV fica em <c>user://</c> e registra hardware,
/// commit, resolução e percentis para permitir comparações entre builds.
/// </remarks>
public sealed partial class PerformanceBench : Node
{
    private const string BenchmarkArgument = "--bench=40enemies";
    private const string DurationPrefix = "--bench-seconds=";
    private const string RevisionPrefix = "--bench-revision=";
    private const string NoOcclusionArgument = "--bench-no-occlusion";
    private const int EnemyCount = 40;
    private const int WarmupSeconds = 5;
    private const int MaxDurationSeconds = 1800;
    private const int BucketsBelowOverflow = 4000;
    private const double HistogramBucketMilliseconds = 0.05;
    private const double FrameBudgetMilliseconds = 16.6;
    private const string ArenaPath = "res://scenes/arena/Arena.tscn";
    private const string GruntDefinitionPath = "res://data/enemies/grunt.tres";
    private static readonly StringName BenchmarkAbilityId = new("benchmark.performance");
    private static readonly AbilityVfxStyle[] VfxStyles =
    [
        AbilityVfxStyle.DashSlash,
        AbilityVfxStyle.Deadeye,
        AbilityVfxStyle.SpinSlash,
        AbilityVfxStyle.RisingSlash,
        AbilityVfxStyle.QuickStepShot,
        AbilityVfxStyle.HeavyLunge,
        AbilityVfxStyle.FanTheHammer,
        AbilityVfxStyle.ExplosiveShot,
    ];

    private enum RunState { WaitingForPool, Warmup, Measuring, Finished }

    private readonly record struct OcclusionMetrics(
        long Ticks,
        long Raycasts,
        double RaycastsPerTick,
        int MaxRaycastsPerTick,
        long Colliders,
        double CollidersPerTick,
        int MaxCollidersPerTick,
        int RayLimit,
        double AverageSearchUsec,
        ulong MaxSearchUsec,
        string ActiveOccluderPaths,
        int HiddenSurfaces);

    private readonly int[] _frameTimeHistogram = new int[BucketsBelowOverflow + 1];
    private readonly CharacterController?[] _spawnedEnemies = new CharacterController[EnemyCount];
    private readonly Vector3[] _nearestSpawnCenters = new Vector3[3];
    private readonly float[] _nearestSpawnCenterDistances = new float[3];
    private readonly Vector3[] _enemySpawnCenters = new Vector3[4];
    private RunState _state;
    private EnemyPool? _pool;
    private EnemyDefinition? _gruntDefinition;
    private CharacterController? _player;
    private CameraOcclusionFader? _occlusionFader;
    private ulong _lastFrameUsec;
    private ulong _measurementStartUsec;
    private long _lastAllocatedBytes;
    private long _allocatedBytes;
    private long _maxFrameAllocationBytes;
    private ulong _measurementDurationUsec;
    private double _warmupElapsedSeconds;
    private double _vfxElapsedSeconds;
    private long _frameSamples;
    private long _overBudgetFrames;
    private long _overflowHistogramSamples;
    private long _allocationFrames;
    private long _benchmarkInstrumentationAllocatedBytes;
    private long _vfxAllocatedBytes;
    private int _nextVfxStyle;
    private double _minStaticMemoryMegabytes = double.PositiveInfinity;
    private double _maxStaticMemoryMegabytes;
    private double _minNodeCount = double.PositiveInfinity;
    private double _maxNodeCount;
    private double _processTimeSumMilliseconds;
    private double _physicsTimeSumMilliseconds;
    private double _navigationTimeSumMilliseconds;
    private double _maxProcessTimeMilliseconds;
    private double _maxPhysicsTimeMilliseconds;
    private double _maxNavigationTimeMilliseconds;
    private double _maxFrameTimeMilliseconds;
    private int _durationSeconds = 60;
    private string _revision = "unknown";
    private bool _isGraphicalRun;
    private bool _skipOcclusionForDiagnostics;
    private string _videoAdapter = "unavailable";
    private int _nearestSpawnCenterCount;

    public override void _Ready()
    {
        if (!ReadArguments())
        {
            GetTree().Quit(2);
            return;
        }

        var arenaScene = GD.Load<PackedScene>(ArenaPath);
        _gruntDefinition = GD.Load<EnemyDefinition>(GruntDefinitionPath);
        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");
        if (arenaScene is null || _gruntDefinition is null || _pool is null)
        {
            Fail("arena, grunt definition ou EnemyPool não carregaram");
            return;
        }

        AddChild(arenaScene.Instantiate());
        _occlusionFader = FindNode<CameraOcclusionFader>(this);
        if (_skipOcclusionForDiagnostics && _occlusionFader is not null)
            _occlusionFader.SetPhysicsProcess(false);

        _player = FindPlayer(this);
        if (_player?.Context?.Health is not { } health)
        {
            Fail("a arena não registrou um personagem jogável com vida");
            return;
        }

        // Mantém os inimigos em combate durante o teste inteiro, sem terminar
        // a partida ou alterar o comportamento dos componentes de IA.
        health.GrantInvulnerability(MaxDurationSeconds + WarmupSeconds + 5f);
        _videoAdapter = RenderingServer.GetVideoAdapterName();
        _isGraphicalRun = !OS.HasFeature("server")
            && !string.IsNullOrWhiteSpace(_videoAdapter)
            && !_videoAdapter.Contains("dummy", StringComparison.OrdinalIgnoreCase);
        _lastFrameUsec = Time.GetTicksUsec();
        _state = RunState.WaitingForPool;

        GD.Print($"[bench] aguardando pool; duração={_durationSeconds}s, commit={_revision}");
    }

    public override void _Process(double delta)
    {
        if (_state is RunState.Finished)
            return;

        if (_state == RunState.WaitingForPool)
        {
            if (_pool is not { IsReady: true })
                return;

            if (!SpawnBenchmarkEnemies())
                return;

            _state = RunState.Warmup;
            _lastFrameUsec = Time.GetTicksUsec();
            GD.Print($"[bench] {EnemyCount} inimigos ativos; aquecimento de {WarmupSeconds}s iniciado");
            return;
        }

        var nowUsec = Time.GetTicksUsec();
        var frameIntervalUsec = nowUsec - _lastFrameUsec;
        _lastFrameUsec = nowUsec;
        var beforeVfxBytes = _state == RunState.Measuring ? GC.GetAllocatedBytesForCurrentThread() : 0L;
        EmitVfx(delta);
        if (_state == RunState.Measuring)
            _vfxAllocatedBytes += Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - beforeVfxBytes);

        if (_state == RunState.Warmup)
        {
            _warmupElapsedSeconds += delta;
            if (_warmupElapsedSeconds >= WarmupSeconds)
                BeginMeasurement(nowUsec);

            return;
        }

        if (_state != RunState.Measuring)
            return;

        SampleFrame(frameIntervalUsec);
        if (nowUsec - _measurementStartUsec >= _measurementDurationUsec)
            FinishMeasurement();
    }

    public override void _ExitTree()
    {
        Engine.TimeScale = 1f;
        if (_pool is null)
            return;

        foreach (var enemy in _spawnedEnemies)
        {
            if (enemy is not null && GodotObject.IsInstanceValid(enemy))
                _pool.Release(enemy);
        }
    }

    private bool ReadArguments()
    {
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith(DurationPrefix, StringComparison.Ordinal))
            {
                var value = argument.AsSpan(DurationPrefix.Length);
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _durationSeconds)
                    || _durationSeconds is < 5 or > MaxDurationSeconds)
                {
                    GD.PrintErr($"[bench] --bench-seconds deve ser um inteiro entre 5 e {MaxDurationSeconds}.");
                    return false;
                }
            }
            else if (argument.StartsWith(RevisionPrefix, StringComparison.Ordinal))
            {
                _revision = argument[RevisionPrefix.Length..];
            }
            else if (argument == NoOcclusionArgument)
            {
                _skipOcclusionForDiagnostics = true;
            }
            else if (argument != BenchmarkArgument)
            {
                GD.PrintErr($"[bench] argumento desconhecido: {argument}");
                return false;
            }
        }

        _measurementDurationUsec = (ulong)_durationSeconds * 1_000_000UL;
        return true;
    }

    private bool SpawnBenchmarkEnemies()
    {
        if (_pool is null || _gruntDefinition is null || _player is null)
            return false;

        if (_pool.GruntPoolSize - _pool.ActiveCount < EnemyCount)
        {
            Fail($"o pool tem apenas {_pool.GruntPoolSize - _pool.ActiveCount} grunts livres; são necessários {EnemyCount}");
            return false;
        }

        var arena = GetChild(0);
        var spawnPoints = arena.GetNodeOrNull<Node3D>("SpawnPoints");
        if (spawnPoints is null || spawnPoints.GetChildCount() == 0)
        {
            Fail("SpawnPoints não contém marcadores na arena urbana");
            return false;
        }

        if (!FindNearbySpawnCenters(spawnPoints, _player.GlobalPosition, _gruntDefinition.DetectionRadius))
            return false;

        var activeBefore = _pool.ActiveCount;
        for (var i = 0; i < EnemyCount; i++)
        {
            const int enemiesPerCenter = EnemyCount / 4;
            var centerIndex = i / enemiesPerCenter;
            var slot = i % enemiesPerCenter;
            var angle = (Mathf.Tau * slot / enemiesPerCenter) + (centerIndex * 0.17f);
            var radius = 3.25f + (slot % 4) * 0.3f;
            var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            var enemy = _pool.Acquire(_gruntDefinition, _enemySpawnCenters[centerIndex] + offset);
            if (enemy is null)
            {
                Fail($"EnemyPool.Acquire falhou no inimigo {i + 1}/{EnemyCount}");
                return false;
            }

            _spawnedEnemies[i] = enemy;
        }

        if (_pool.ActiveCount != activeBefore + EnemyCount)
        {
            Fail($"ActiveCount deveria subir {EnemyCount}; era {activeBefore}, ficou {_pool.ActiveCount}");
            return false;
        }

        return true;
    }

    private bool FindNearbySpawnCenters(Node3D spawnPoints, Vector3 playerPosition, float detectionRadius)
    {
        Array.Fill(_nearestSpawnCenterDistances, float.PositiveInfinity);
        _nearestSpawnCenterCount = 0;
        const float MaximumSpawnOffsetMeters = 4.15f;
        var maxDistanceFromPlayer = detectionRadius - MaximumSpawnOffsetMeters;
        if (maxDistanceFromPlayer <= 0f)
        {
            Fail("o raio de percepção do grunt é pequeno demais para espalhar o cenário de benchmark");
            return false;
        }
        var maxDistanceSquared = maxDistanceFromPlayer * maxDistanceFromPlayer;

        for (var i = 0; i < spawnPoints.GetChildCount(); i++)
        {
            if (spawnPoints.GetChild(i) is not Node3D marker)
                continue;

            var candidate = marker.GlobalPosition;
            var distanceSquared = candidate.DistanceSquaredTo(playerPosition);
            if (distanceSquared > maxDistanceSquared)
                continue;

            for (var nearestSlot = 0; nearestSlot < _nearestSpawnCenters.Length; nearestSlot++)
            {
                if (distanceSquared >= _nearestSpawnCenterDistances[nearestSlot])
                    continue;

                for (var shift = _nearestSpawnCenters.Length - 1; shift > nearestSlot; shift--)
                {
                    _nearestSpawnCenters[shift] = _nearestSpawnCenters[shift - 1];
                    _nearestSpawnCenterDistances[shift] = _nearestSpawnCenterDistances[shift - 1];
                }

                _nearestSpawnCenters[nearestSlot] = candidate;
                _nearestSpawnCenterDistances[nearestSlot] = distanceSquared;
                if (_nearestSpawnCenterCount < _nearestSpawnCenters.Length)
                    _nearestSpawnCenterCount++;
                break;
            }
        }

        if (_nearestSpawnCenterCount < _nearestSpawnCenters.Length)
        {
            Fail($"a arena precisa de três marcadores a no máximo {maxDistanceFromPlayer:F1} m do personagem para exercitar a IA em combate");
            return false;
        }

        // O jogador fica parado no benchmark; grupos dentro do raio de detecção
        // mantêm os quarenta cérebros em perseguição/combate durante a medição.
        _enemySpawnCenters[0] = playerPosition;
        for (var i = 0; i < _nearestSpawnCenters.Length; i++)
            _enemySpawnCenters[i + 1] = _nearestSpawnCenters[i];

        return true;
    }

    private void BeginMeasurement(ulong nowUsec)
    {
        _state = RunState.Measuring;
        _measurementStartUsec = nowUsec;
        _lastFrameUsec = nowUsec;
        _lastAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
        _occlusionFader?.ResetarDiagnosticosDoBenchmark();
        GD.Print($"[bench] aquecimento concluído; medindo {_durationSeconds}s");
    }

    private void SampleFrame(ulong frameIntervalUsec)
    {
        var instrumentationBytesBefore = GC.GetAllocatedBytesForCurrentThread();
        var frameTimeMilliseconds = frameIntervalUsec / 1000.0;
        var bucket = (int)(frameTimeMilliseconds / HistogramBucketMilliseconds);
        if (bucket >= BucketsBelowOverflow)
        {
            bucket = BucketsBelowOverflow;
            _overflowHistogramSamples++;
        }

        _frameTimeHistogram[bucket]++;
        _frameSamples++;
        _maxFrameTimeMilliseconds = Math.Max(_maxFrameTimeMilliseconds, frameTimeMilliseconds);
        if (frameTimeMilliseconds > FrameBudgetMilliseconds)
            _overBudgetFrames++;

        var allocatedNow = GC.GetAllocatedBytesForCurrentThread();
        var allocatedThisInterval = Math.Max(0L, allocatedNow - _lastAllocatedBytes);
        _lastAllocatedBytes = allocatedNow;
        _allocatedBytes += allocatedThisInterval;
        if (allocatedThisInterval > 0)
        {
            _allocationFrames++;
            _maxFrameAllocationBytes = Math.Max(_maxFrameAllocationBytes, allocatedThisInterval);
        }

        var processMilliseconds = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
        var physicsMilliseconds = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
        var navigationMilliseconds = Performance.GetMonitor(Performance.Monitor.TimeNavigationProcess) * 1000.0;
        _processTimeSumMilliseconds += processMilliseconds;
        _physicsTimeSumMilliseconds += physicsMilliseconds;
        _navigationTimeSumMilliseconds += navigationMilliseconds;
        _maxProcessTimeMilliseconds = Math.Max(_maxProcessTimeMilliseconds, processMilliseconds);
        _maxPhysicsTimeMilliseconds = Math.Max(_maxPhysicsTimeMilliseconds, physicsMilliseconds);
        _maxNavigationTimeMilliseconds = Math.Max(_maxNavigationTimeMilliseconds, navigationMilliseconds);

        var staticMemoryMegabytes = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / (1024.0 * 1024.0);
        var nodeCount = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        _minStaticMemoryMegabytes = Math.Min(_minStaticMemoryMegabytes, staticMemoryMegabytes);
        _maxStaticMemoryMegabytes = Math.Max(_maxStaticMemoryMegabytes, staticMemoryMegabytes);
        _minNodeCount = Math.Min(_minNodeCount, nodeCount);
        _maxNodeCount = Math.Max(_maxNodeCount, nodeCount);
        _benchmarkInstrumentationAllocatedBytes += Math.Max(0L,
            GC.GetAllocatedBytesForCurrentThread() - instrumentationBytesBefore);
    }

    private void EmitVfx(double delta)
    {
        _vfxElapsedSeconds += delta;
        while (_vfxElapsedSeconds >= 0.25)
        {
            _vfxElapsedSeconds -= 0.25;
            var style = VfxStyles[_nextVfxStyle];
            _nextVfxStyle = (_nextVfxStyle + 1) % VfxStyles.Length;
            var tint = style switch
            {
                AbilityVfxStyle.DashSlash => new Color(0.2f, 0.85f, 1f),
                AbilityVfxStyle.Deadeye => new Color(1f, 0.91f, 0.3f),
                AbilityVfxStyle.SpinSlash => new Color(0.85f, 0.45f, 1f),
                AbilityVfxStyle.RisingSlash => new Color(0.25f, 1f, 0.55f),
                AbilityVfxStyle.QuickStepShot => new Color(0.3f, 0.72f, 1f),
                AbilityVfxStyle.HeavyLunge => new Color(1f, 0.42f, 0.16f),
                AbilityVfxStyle.FanTheHammer => new Color(1f, 0.28f, 0.47f),
                _ => new Color(1f, 0.2f, 0.04f),
            };
            ServiceLocator.Events.RaiseAbilityCast(new AbilityCastPresentationEvent(
                BenchmarkAbilityId,
                style,
                _player?.GlobalPosition ?? Vector3.Zero,
                Vector3.Forward,
                tint));
        }
    }

    private void FinishMeasurement()
    {
        _state = RunState.Finished;
        var p50 = PercentileMilliseconds(0.50);
        var p99 = PercentileMilliseconds(0.99);
        var overBudgetPercent = _frameSamples == 0 ? 0d : (double)_overBudgetFrames / _frameSamples * 100d;
        var processAverage = Average(_processTimeSumMilliseconds);
        var physicsAverage = Average(_physicsTimeSumMilliseconds);
        var navigationAverage = Average(_navigationTimeSumMilliseconds);
        var graphicalResult = _isGraphicalRun
            ? p99 <= FrameBudgetMilliseconds ? "PASS" : "FAIL"
            : "NOT_RUN_HEADLESS";
        var occlusion = CaptureOcclusionMetrics();

        var reportPath = $"user://bench-40enemies-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{OS.GetProcessId()}.csv";
        WriteReport(reportPath, p50, p99, overBudgetPercent, processAverage, physicsAverage, navigationAverage, graphicalResult, occlusion);

        GD.Print($"[bench] resultado gráfico={graphicalResult}; frames={_frameSamples}; p50={p50:F2}ms; p99={p99:F2}ms; max={_maxFrameTimeMilliseconds:F2}ms; acima de {FrameBudgetMilliseconds:F1}ms={overBudgetPercent:F2}%; processo médio={processAverage:F2}ms; física média={physicsAverage:F2}ms; navegação média={navigationAverage:F2}ms");
        GD.Print($"[bench] alocações gerenciadas={_allocatedBytes} bytes em {_allocationFrames}/{_frameSamples} intervalos; pico={_maxFrameAllocationBytes} bytes; nós={_minNodeCount:F0}..{_maxNodeCount:F0}; memória estática={_minStaticMemoryMegabytes:F1}..{_maxStaticMemoryMegabytes:F1} MiB");
        GD.Print($"[bench] alocação atribuída à sonda={_benchmarkInstrumentationAllocatedBytes} bytes; ao disparo de VFX={_vfxAllocatedBytes} bytes");
        GD.Print($"[bench] oclusão={occlusion.Ticks} ticks; testes de raio={occlusion.Raycasts} (média {occlusion.RaycastsPerTick:F2}/tick; máx {occlusion.MaxRaycastsPerTick}); colisores encontrados={occlusion.Colliders} (média {occlusion.CollidersPerTick:F2}/tick; máx {occlusion.MaxCollidersPerTick}); busca média/máxima={occlusion.AverageSearchUsec:F1}/{occlusion.MaxSearchUsec} μs; limite por tick={occlusion.RayLimit}");
        GD.Print($"[bench] fade atual: caminhos={occlusion.ActiveOccluderPaths}; superfícies={occlusion.HiddenSurfaces}");
        var resolutionBase = ConfiguredViewportResolution();
        var windowResolution = DisplayServer.WindowGetSize();
        GD.Print($"[bench] relatório={ProjectSettings.GlobalizePath(reportPath)}; GPU={_videoAdapter}; OS={OS.GetName()}; CPU={OS.GetProcessorName()}; cores={OS.GetProcessorCount()}; viewport base={resolutionBase.X}x{resolutionBase.Y}; janela={windowResolution.X}x{windowResolution.Y}");

        if (_isGraphicalRun && graphicalResult == "FAIL")
            GetTree().Quit(1);
        else
            GetTree().Quit();
    }

    private void WriteReport(
        string reportPath,
        double p50,
        double p99,
        double overBudgetPercent,
        double processAverage,
        double physicsAverage,
        double navigationAverage,
        string graphicalResult,
        OcclusionMetrics occlusion)
    {
        using var file = FileAccess.Open(reportPath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PrintErr($"[bench] não consegui gravar o relatório: {FileAccess.GetOpenError()}");
            return;
        }

        var resolutionBase = ConfiguredViewportResolution();
        var windowResolution = DisplayServer.WindowGetSize();
        file.StoreLine("utc,revision,duration_seconds,frames,p50_frame_ms,p99_frame_ms,max_frame_ms,budget_ms,over_budget_percent,process_avg_ms,physics_avg_ms,navigation_avg_ms,managed_allocated_bytes,allocation_frames,max_frame_allocation_bytes,histogram_overflow_frames,node_count_min,node_count_max,static_memory_min_mib,static_memory_max_mib,os,cpu,cores,gpu,viewport_width,viewport_height,window_width,window_height,occlusion_enabled,graphical_result,occlusion_ticks,occlusion_raycast_checks,occlusion_raycast_checks_avg,occlusion_raycast_checks_max,occlusion_colliders_hit,occlusion_colliders_hit_avg,occlusion_colliders_hit_max,occlusion_ray_limit,occlusion_query_avg_us,occlusion_query_max_us");
        file.StoreLine(string.Join(",",
            DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Csv(_revision),
            _durationSeconds.ToString(CultureInfo.InvariantCulture),
            _frameSamples.ToString(CultureInfo.InvariantCulture),
            Number(p50), Number(p99), Number(_maxFrameTimeMilliseconds), Number(FrameBudgetMilliseconds), Number(overBudgetPercent),
            Number(processAverage), Number(physicsAverage), Number(navigationAverage),
            _allocatedBytes.ToString(CultureInfo.InvariantCulture),
            _allocationFrames.ToString(CultureInfo.InvariantCulture),
            _maxFrameAllocationBytes.ToString(CultureInfo.InvariantCulture),
            _overflowHistogramSamples.ToString(CultureInfo.InvariantCulture),
            _minNodeCount.ToString("F0", CultureInfo.InvariantCulture), _maxNodeCount.ToString("F0", CultureInfo.InvariantCulture),
            Number(_minStaticMemoryMegabytes), Number(_maxStaticMemoryMegabytes),
            Csv(OS.GetName()), Csv(OS.GetProcessorName()), OS.GetProcessorCount().ToString(CultureInfo.InvariantCulture),
            Csv(_videoAdapter), resolutionBase.X.ToString(CultureInfo.InvariantCulture), resolutionBase.Y.ToString(CultureInfo.InvariantCulture),
            windowResolution.X.ToString(CultureInfo.InvariantCulture), windowResolution.Y.ToString(CultureInfo.InvariantCulture),
            (!_skipOcclusionForDiagnostics).ToString(CultureInfo.InvariantCulture), graphicalResult,
            occlusion.Ticks.ToString(CultureInfo.InvariantCulture),
            occlusion.Raycasts.ToString(CultureInfo.InvariantCulture),
            Number(occlusion.RaycastsPerTick),
            occlusion.MaxRaycastsPerTick.ToString(CultureInfo.InvariantCulture),
            occlusion.Colliders.ToString(CultureInfo.InvariantCulture),
            Number(occlusion.CollidersPerTick),
            occlusion.MaxCollidersPerTick.ToString(CultureInfo.InvariantCulture),
            occlusion.RayLimit.ToString(CultureInfo.InvariantCulture),
            Number(occlusion.AverageSearchUsec),
            occlusion.MaxSearchUsec.ToString(CultureInfo.InvariantCulture)));
    }

    private OcclusionMetrics CaptureOcclusionMetrics()
    {
        var fader = _occlusionFader;
        var ticks = fader?.TotalDeTicksDeOclusao ?? 0;
        return new OcclusionMetrics(
            ticks,
            fader?.TotalDeTestesDeRaio ?? 0,
            ticks == 0 ? 0d : (double)fader!.TotalDeTestesDeRaio / ticks,
            fader?.MaximoDeTestesDeRaioPorTick ?? 0,
            fader?.TotalDeColisoresEncontrados ?? 0,
            ticks == 0 ? 0d : (double)fader!.TotalDeColisoresEncontrados / ticks,
            fader?.MaximoDeColisoresPorTick ?? 0,
            fader?.LimiteDeColisoresNoRaio ?? 0,
            ticks == 0 ? 0d : (double)fader!.TempoTotalDeBuscaUsec / ticks,
            fader?.TempoMaximoDeBuscaUsec ?? 0,
            fader?.CaminhosDosOclusoresOcultosAgora ?? "indisponível",
            fader?.SuperficiesOcultasAgora ?? 0);
    }

    private static Vector2I ConfiguredViewportResolution() => new(
        ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32(),
        ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32());

    private double PercentileMilliseconds(double percentile)
    {
        if (_frameSamples == 0)
            return 0d;

        var rank = (long)Math.Ceiling(_frameSamples * percentile);
        long cumulative = 0;
        for (var bucket = 0; bucket < _frameTimeHistogram.Length; bucket++)
        {
            cumulative += _frameTimeHistogram[bucket];
            if (cumulative < rank)
                continue;

            if (bucket == BucketsBelowOverflow)
                return _maxFrameTimeMilliseconds;

            return (bucket + 0.5) * HistogramBucketMilliseconds;
        }

        return _maxFrameTimeMilliseconds;
    }

    private double Average(double total) => _frameSamples == 0 ? 0d : total / _frameSamples;

    private static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static CharacterController? FindPlayer(Node node)
    {
        if (node is CharacterController { Team: Team.Player, Context: { Health: not null } } player)
            return player;

        foreach (var child in node.GetChildren())
        {
            var result = FindPlayer(child);
            if (result is not null)
                return result;
        }

        return null;
    }

    private static T? FindNode<T>(Node node) where T : Node
    {
        if (node is T matched)
            return matched;

        foreach (var child in node.GetChildren())
        {
            var found = FindNode<T>(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    private void Fail(string message)
    {
        _state = RunState.Finished;
        GD.PrintErr($"[bench] FALHA: {message}");
        GetTree().Quit(2);
    }
}
