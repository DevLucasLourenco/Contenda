using System;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Health;
using Contenda.Core;
using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>
/// O modo horda: cinco ondas, um chefe na última. Vence quem limpa a última
/// onda; perde quem morre antes. Ticket 28, spec 10 §2.
/// </summary>
/// <remarks>
/// Só orquestra: quem sabe QUANDO e ONDE cada inimigo nasce é o
/// <see cref="WaveDirector"/>/<see cref="SpawnDirector"/> (ticket 27), e quem
/// desenha a tela de resultado é o ticket 29 -- este nó guarda as regras de
/// vitória/derrota e o resultado em <see cref="GameSession.LastResult"/>.
///
/// Fica inerte até <see cref="StartMatch"/> (ou <see cref="AutoStart"/>):
/// `Arena.tscn` é a cena principal e a base de todos os probes, e nenhum deles
/// espera uma partida acontecendo sozinha.
///
/// Sem a contagem regressiva 3-2-1 da spec §2: é apresentação de HUD, e o HUD
/// final é o ticket 33.
/// </remarks>
public sealed partial class HordeGameMode : Node, IGameMode
{
    /// <summary>A progressão da partida.</summary>
    [Export] public WaveSetDefinition? WaveSet { get; set; }

    [Export] public NodePath WaveDirectorPath { get; set; } = new();

    /// <summary>Começa a partida sozinho ao entrar na árvore. Falso na arena base; verdadeiro na cena jogável.</summary>
    [Export] public bool AutoStart { get; set; }

    private WaveDirector? _waveDirector;
    private EnemyPool? _pool;
    private HealthComponent? _vidaDoJogador;
    private CharacterController? _jogador;

    private int _ondasLimpas;
    private int _abates;
    private float _duracao;
    private bool _prontoParaComecar;

    private static readonly StringName IdDoModo = new("horde");

    public StringName Id => IdDoModo;

    public GameModeState State { get; private set; } = GameModeState.Starting;

    public event Action<GameModeState>? StateChanged;

    public event Action<GameModeResult>? MatchEnded;

    public override void _Ready()
    {
        _waveDirector = GetNodeOrNull<WaveDirector>(WaveDirectorPath);
        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");

        if (_waveDirector is null || _pool is null)
            GD.PushError($"{Name}: WaveDirectorPath ou EnemyPool não resolveram.");

        ServiceLocator.Events.EnemyKilled += AoMatarInimigo;

        if (AutoStart && WaveSet is { } conjunto)
        {
            Initialize(new GameModeConfig(conjunto));
            StartMatch();
        }
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.EnemyKilled -= AoMatarInimigo;
        DesligarDoJogador();

        if (_waveDirector is not null)
            _waveDirector.WaveCleared -= AoLimparOnda;
    }

    public void Initialize(GameModeConfig config)
    {
        WaveSet = config.WaveSet;
    }

    /// <summary>Pede o início. A partida só começa de verdade quando o estoque de inimigos estiver pronto.</summary>
    public void StartMatch()
    {
        if (WaveSet is null || _waveDirector is null || _pool is null)
        {
            GD.PushError($"{Name}: sem WaveSet, WaveDirector ou EnemyPool -- não dá para começar.");
            return;
        }

        PrewarmarEspecies(WaveSet);
        _prontoParaComecar = true;
    }

    /// <remarks>
    /// Espera o prewarm do grunt (autoload, adiado por um quadro) terminar
    /// antes de largar a primeira onda -- sem isto, um `StartMatch` no
    /// `_Ready` da arena pediria `Acquire` de um pool ainda vazio.
    /// </remarks>
    public override void _PhysicsProcess(double delta)
    {
        if (_prontoParaComecar && State == GameModeState.Starting && _pool is { IsReady: true })
        {
            _prontoParaComecar = false;
            ComecarDeVerdade();
            return;
        }

        if (State == GameModeState.Playing)
            _duracao += (float)delta;
    }

    public void EndMatch(GameModeResult result)
    {
        if (State == GameModeState.Ended)
            return;

        _waveDirector?.Abort();
        DesligarDoJogador();

        Mudar(GameModeState.Ended);
        ServiceLocator.Session.LastResult = result;
        MatchEnded?.Invoke(result);
    }

    /// <summary>Cria o estoque das espécies que o conjunto usa e o grunt de boot ainda não cobre. Spec 10 §2.</summary>
    private void PrewarmarEspecies(WaveSetDefinition conjunto)
    {
        foreach (var onda in conjunto.Waves)
        {
            foreach (var entrada in onda.Entries)
                GarantirEstoque(entrada.Enemy);

            GarantirEstoque(onda.ReinforcementEnemy);
        }
    }

    private void GarantirEstoque(EnemyDefinition? definicao)
    {
        if (definicao is null || _pool is null || _pool.IsPrewarmed(definicao))
            return;

        if (definicao.Scene is null)
        {
            GD.PushError($"{Name}: um EnemyDefinition do conjunto não tem Scene e não foi prewarmado.");
            return;
        }

        _pool.Prewarm(definicao.Scene, definicao, definicao.PoolSize);
    }

    private void ComecarDeVerdade()
    {
        if (WaveSet is null || _waveDirector is null)
            return;

        _jogador = ServiceLocator.Session.PlayerBody;
        _vidaDoJogador = _jogador?.Context?.Health;
        if (_vidaDoJogador is not null)
            _vidaDoJogador.Died += AoJogadorMorrer;

        _waveDirector.WaveCleared += AoLimparOnda;

        _ondasLimpas = 0;
        _abates = 0;
        _duracao = 0f;

        Mudar(GameModeState.Playing);
        _waveDirector.Begin(WaveSet);
    }

    private void AoLimparOnda(WaveDefinition onda, int indice)
    {
        _ondasLimpas++;

        if (WaveSet is not null && indice >= WaveSet.Waves.Length - 1)
            EndMatch(MontarResultado(vitoria: true));
    }

    private void AoJogadorMorrer(DamageInfo golpe) => EndMatch(MontarResultado(vitoria: false));

    private void AoMatarInimigo(EnemyKilledEvent evento)
    {
        if (State == GameModeState.Playing)
            _abates++;
    }

    private GameModeResult MontarResultado(bool vitoria) => new(
        Victory: vitoria,
        Score: 0,
        WavesCleared: _ondasLimpas,
        EnemiesKilled: _abates,
        DurationSeconds: _duracao,
        CharacterId: _jogador?.Definition?.Id ?? new StringName("desconhecido"));

    private void DesligarDoJogador()
    {
        if (_vidaDoJogador is not null)
            _vidaDoJogador.Died -= AoJogadorMorrer;

        _vidaDoJogador = null;
    }

    private void Mudar(GameModeState novo)
    {
        State = novo;
        StateChanged?.Invoke(novo);
    }
}
