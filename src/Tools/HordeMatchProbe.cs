using System.Collections.Generic;
using System.IO;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.GameModes;
using Contenda.GameModes.Horde;
using Contenda.UI.HUD;
using Contenda.UI.Menus;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Joga uma partida inteira do modo horda na árvore de nós real, com o
/// `waveset_default.tres` de verdade, e confere o que o ticket 28 promete: as
/// cinco ondas rodam do início ao fim, a composição muda (não só a
/// quantidade), o chefe chega pela praça com reforços descendo das vielas, e
/// a partida termina em vitória -- ou em derrota, se o jogador cair antes.
/// </summary>
/// <remarks>
/// Um "jogador" automático mata todo inimigo assim que ele aparece (menos o
/// chefe, que fica vivo até os reforços chegarem) -- o que este probe mede é a
/// ORQUESTRAÇÃO da partida (ondas, composição, chefe, fim), não a dificuldade
/// dela; balanceamento e a duração de 8-12 minutos dependem de gente jogando
/// (ticket 37). O jogador de verdade fica invulnerável na vitória, para o
/// resultado depender só do bot.
///
/// <see cref="Scenario"/>: "victory" (padrão), "defeat", ou "swap" (troca o conjunto
/// de ondas por outro, em memória, e confere que a partida segue o novo).
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/HordeMatchProbe.tscn
/// godot --headless --path . --scene res://scenes/debug/HordeDefeatProbe.tscn
/// godot --headless --path . --scene res://scenes/debug/HordeSwapProbe.tscn
/// </code>
/// </remarks>
public sealed partial class HordeMatchProbe : Node
{
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    [Export] public string Scenario { get; set; } = "victory";

    /// <summary>Acelera o relógio do jogo -- a partida de verdade tem ~1 minuto de espera só de spawn e respiro.</summary>
    [Export(PropertyHint.Range, "1,10,0.5")] public float VelocidadeDoJogo { get; set; } = 3f;

    // Praça e ruas laterais em Arena.tscn: Spawn7 (praça) e Spawn3/Spawn4 (ruas laterais).
    private static readonly Vector3 Praca = new(0f, -0.9f, 0f);

    private const string CaminhoDoPerfil = "user://probe_profile.cfg";

    private const int LimiteDeQuadros = 60 * 400;
    // A onda 5 planeja 6 grunts de entrada; passar disso são reforços.
    private const int AbatesDeNaoChefeParaMatarOChefe = 10;

    private readonly List<string> _falhas = [];
    private HordeGameMode? _modo;
    private WaveDirector? _ondas;
    private EnemyPool? _pool;
    private CharacterController? _jogador;

    private int _quadro;
    private bool _comecou;
    private bool _resultadoVisto;
    private GameModeResult _resultado;

    // Por onda: quais espécies apareceram, e onde as notáveis nasceram.
    private readonly Dictionary<int, HashSet<string>> _especiesPorOnda = [];
    private readonly HashSet<CharacterController> _jaRegistrados = [];
    private Vector3? _ondaDoBrutoEm;
    private Vector3? _ondaDoChefeEm;
    private bool _pincaRespeitada = true;

    // Chefe.
    private bool _chefeVisto;
    private bool _chefeMorto;
    private int _abatesNaoChefeNaOnda5;
    private bool _viuEventoDeChefe;

    // Placar: o bônus de onda sem apanhar tem que chegar como +250 exatos depois de cada onda limpa.
    private int _placarUltimo;
    private int _placarAntesDoBonus = -1;
    private int _bonusesVistos;
    private int _quadroDaMorteDoChefe = -1;
    private int _reforcosQuandoOChefeCaiu = -1;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[partida] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);
        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");
        _jogador = ServiceLocator.Session.PlayerBody;

        if (_modo is null || _ondas is null || _pool is null || _jogador is null)
        {
            GD.PrintErr("[partida] FALHA: HordeGameMode, WaveDirector, EnemyPool ou jogador não resolveram.");
            GetTree().Quit(1);
            return;
        }

        // Um perfil PRÓPRIO do probe -- partidas de teste nunca sujam o de verdade.
        _modo.ProfilePath = CaminhoDoPerfil;
        var perfilReal = ProjectSettings.GlobalizePath(CaminhoDoPerfil);
        if (File.Exists(perfilReal))
            File.Delete(perfilReal);

        _modo.MatchEnded += resultado =>
        {
            _resultado = resultado;
            _resultadoVisto = true;
        };

        ServiceLocator.Events.EnemyKilled += AoMatar;
        ServiceLocator.Events.ScoreChanged += AoMudarPlacar;
        _ondas.WaveCleared += (_, _) => _placarAntesDoBonus = _placarUltimo;

        if (Scenario != "defeat")
            Engine.TimeScale = VelocidadeDoJogo;

        GD.Print($"[partida] cena pronta ({Scenario})");
    }

    public override void _ExitTree()
    {
        Engine.TimeScale = 1f;
        ServiceLocator.Events.EnemyKilled -= AoMatar;
        ServiceLocator.Events.ScoreChanged -= AoMudarPlacar;
    }

    private void AoMudarPlacar(ScoreChangedEvent evento)
    {
        if (_placarAntesDoBonus >= 0)
        {
            if (evento.Score - _placarAntesDoBonus == (_modo?.ScoreRulesData?.FlawlessWaveBonus ?? -1))
                _bonusesVistos++;

            _placarAntesDoBonus = -1;
        }

        _placarUltimo = evento.Score;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_modo is null || _pool is null || _jogador is null || _ondas is null)
            return;

        if (!_pool.IsReady)
            return;

        _quadro++;

        if (!_comecou)
        {
            _comecou = true;

            if (Scenario != "defeat")
                _jogador.Context!.Health!.GrantInvulnerability(99999f);

            if (Scenario == "swap")
                _modo.Initialize(new GameModeConfig(ConjuntoDeUmaOnda()));

            _modo.StartMatch();
            return;
        }

        if (Scenario == "defeat")
            TickDerrota();
        else
            TickVitoria();
    }

    // --- derrota: morrer encerra a partida ---

    private int _quadroDaMorte = -1;

    private void TickDerrota()
    {
        if (_modo!.State == GameModeState.Playing && _quadroDaMorte < 0)
        {
            // Deixa a onda 1 começar de verdade antes de matar o jogador.
            if (_ondas!.CurrentWave is null)
                return;

            _quadroDaMorte = _quadro;
            _jogador!.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: 99999f, Type: DamageType.Physical, HitPoint: _jogador.GlobalPosition,
                Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL, SourceTag: "match_probe", IsCritical: false));
            return;
        }

        if (_quadroDaMorte < 0)
            return;

        if (!_resultadoVisto && _quadro - _quadroDaMorte < 120)
            return;

        Verificar(_resultadoVisto, "morrer deveria encerrar a partida (MatchEnded)");
        Verificar(_modo.State == GameModeState.Ended, "o modo deveria estar Ended depois da morte do jogador");
        Verificar(!_resultado.Victory, "morrer deveria ser derrota");
        Verificar(_resultado.WavesCleared == 0, $"nenhuma onda limpa antes de morrer; veio {_resultado.WavesCleared}");
        Verificar(ServiceLocator.Session.LastResult is { Victory: false },
            "GameSession.LastResult deveria guardar a derrota");
        Verificar(_ondas!.CurrentWave is null, "o WaveDirector deveria ter parado (Abort) com o fim da partida");

        Concluir();
    }

    // --- vitória: cinco ondas, chefe, fim ---

    private void TickVitoria()
    {
        if (_resultadoVisto)
        {
            VerificarVitoria();
            return;
        }

        if (_quadro > LimiteDeQuadros)
        {
            Verificar(false, $"a partida não terminou em {LimiteDeQuadros} quadros -- travou numa onda? "
                + $"(onda atual: {_ondas!.CurrentWaveIndex}, restantes: {_ondas.EnemiesRemaining}, ativos: {_pool!.ActiveCount})");
            Concluir();
            return;
        }

        if (_modo!.State != GameModeState.Playing || _ondas!.CurrentWave is null)
            return;

        // Alguns quadros depois da queda do chefe, o número de reforços já
        // entregues não pode mais subir.
        if (_quadroDaMorteDoChefe > 0 && _reforcosQuandoOChefeCaiu < 0 && _quadro - _quadroDaMorteDoChefe >= 5)
            _reforcosQuandoOChefeCaiu = _ondas.ReinforcementsSpawned;

        var onda = _ondas.CurrentWaveIndex;
        if (!_especiesPorOnda.ContainsKey(onda))
            _especiesPorOnda[onda] = [];

        foreach (var no in GetTree().GetNodesInGroup("damageable"))
        {
            if (no is not CharacterController { Visible: true } c || c.Team != Team.Enemy
                || c.Context is not { Health: { } vida, EnemyBrain: { } cerebro })
                continue;

            if (!vida.IsAlive)
            {
                _jaRegistrados.Remove(c);
                continue;
            }

            var id = c.Definition?.Id.ToString() ?? "?";

            // Instância reciclada pelo pool volta como "novo" spawn: só vale
            // como já visto enquanto vive.
            if (_jaRegistrados.Add(c))
                RegistrarSpawn(onda, id, cerebro.Definition, c.GlobalPosition);

            if (cerebro.Definition.IsBoss && !DeveMatarOChefe())
                continue;

            if (cerebro.Definition.IsBoss)
            {
                _chefeMorto = true;
                _quadroDaMorteDoChefe = _quadro;
            }

            vida.ApplyDamage(new DamageInfo(
                Amount: 99999f, Type: DamageType.Physical, HitPoint: c.GlobalPosition,
                Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL, SourceTag: "match_probe", IsCritical: false));
        }
    }

    private void RegistrarSpawn(int onda, string id, EnemyDefinition definicao, Vector3 posicao)
    {
        _especiesPorOnda[onda].Add(id);

        // Posição da primeira aparição de cada instância -- o bot mata no
        // quadro seguinte, então ainda está (quase) onde nasceu.
        if (definicao.IsBoss)
        {
            _chefeVisto = true;
            _ondaDoChefeEm ??= posicao;
        }
        else if (definicao.IsElite)
        {
            _ondaDoBrutoEm ??= posicao;
        }

        // Onda 3 (índice 2): pinça pelas ruas laterais -- grunt/runner nascem em x=±14, z=0.
        if (onda == 2 && (id is "grunt" or "runner") && (Mathf.Abs(posicao.X) < 10f || Mathf.Abs(posicao.Z) > 4f))
            _pincaRespeitada = false;
    }

    private bool DeveMatarOChefe()
        => _abatesNaoChefeNaOnda5 >= AbatesDeNaoChefeParaMatarOChefe;

    private void AoMatar(EnemyKilledEvent evento)
    {
        if (evento.IsBoss)
            _viuEventoDeChefe = true;

        if (evento.IsBoss || _ondas?.CurrentWaveIndex != 4)
            return;

        _abatesNaoChefeNaOnda5++;
    }

    private WaveSetDefinition ConjuntoDeUmaOnda()
    {
        var grunt = GD.Load<EnemyDefinition>("res://data/enemies/grunt.tres");

        return new WaveSetDefinition
        {
            DisplayName = "Uma onda so",
            Waves =
            [
                new WaveDefinition
                {
                    DisplayName = "UNICA",
                    Entries = [new EnemySpawnEntry { Enemy = grunt, Count = 2 }],
                    SpawnInterval = 0.3f,
                    CompletionDelay = 0.5f,
                },
            ],
        };
    }

    private void VerificarTroca()
    {
        Verificar(_resultado.Victory, "limpar a única onda do conjunto trocado deveria ser vitória");
        Verificar(_resultado.WavesCleared == 1, $"o conjunto trocado tem 1 onda; a partida limpou {_resultado.WavesCleared}");
        Verificar(_resultado.EnemiesKilled == 2, $"o conjunto trocado planeja 2 inimigos; abateu {_resultado.EnemiesKilled}");
        Concluir();
    }

    private void VerificarVitoria()
    {
        if (Scenario == "swap")
        {
            VerificarTroca();
            return;
        }

        Verificar(_resultado.Victory, "limpar a última onda deveria ser vitória");
        Verificar(_resultado.WavesCleared == 5, $"deveria ter limpado 5 ondas; limpou {_resultado.WavesCleared}");
        Verificar(_resultado.EnemiesKilled >= 48,
            $"deveria ter abatido pelo menos os 48 inimigos planejados; abateu {_resultado.EnemiesKilled}");
        Verificar(_resultado.DurationSeconds > 0f, "a partida deveria ter duração");
        Verificar(!string.IsNullOrEmpty(_resultado.CharacterId.ToString()) && _resultado.CharacterId.ToString() != "desconhecido",
            "o resultado deveria dizer quem o jogador era");
        Verificar(ServiceLocator.Session.LastResult is { Victory: true }, "GameSession.LastResult deveria guardar a vitória");

        Verificar(_ondas!.CurrentWave is null, "o WaveDirector deveria ter parado com o fim da partida");
        Verificar(_bonusesVistos == 5,
            $"cada uma das 5 ondas limpas sem apanhar deveria render +250; o bônus chegou {_bonusesVistos} vezes");
        Verificar(_resultado.Score >= 500, $"o placar deveria somar ao menos o chefe (500); somou {_resultado.Score}");

        var tela = Encontrar<ResultsScreen>(GetTree().Root);
        Verificar(tela is { IsShowing: true, TitleText: "VITÓRIA" },
            $"a tela de resultado deveria mostrar VITÓRIA; mostra \"{tela?.TitleText}\"");

        var hud = Encontrar<ScoreDisplay>(GetTree().Root);
        Verificar(hud?.ScoreText == "SCORE  " + ScoreDisplay.FormatarPontos(_resultado.Score),
            $"o HUD deveria mostrar o placar final ({_resultado.Score}); mostra \"{hud?.ScoreText}\"");

        // Composição: cada onda traz algo que a anterior não trazia.
        Verificar(Especies(0).SetEquals(["grunt"]), $"onda 1 deveria ter só grunt; teve {string.Join(",", Especies(0))}");
        Verificar(Especies(1).Contains("runner"), "onda 2 deveria introduzir o runner");
        Verificar(Especies(2).Contains("shooter"), "onda 3 deveria introduzir o shooter");
        Verificar(Especies(3).Contains("brute"), "onda 4 deveria trazer o brute (elite)");
        Verificar(Especies(4).Contains("warlord"), "onda 5 deveria trazer o warlord (chefe)");

        // Funil.
        Verificar(_ondaDoBrutoEm is { } b && b.DistanceTo(Praca) < 5f,
            $"o brute deveria nascer na praça; nasceu em {_ondaDoBrutoEm}");
        Verificar(_ondaDoChefeEm is { } c && c.DistanceTo(Praca) < 5f,
            $"o chefe deveria nascer na praça; nasceu em {_ondaDoChefeEm}");
        Verificar(_pincaRespeitada, "a onda 3 deveria nascer pelas ruas laterais (pinça)");

        // Chefe e reforços.
        Verificar(_chefeVisto, "o chefe deveria ter nascido");
        Verificar(_viuEventoDeChefe, "a morte do chefe deveria anunciar EnemyKilled com IsBoss (é o que para os reforços)");
        Verificar(_ondas.ReinforcementsSpawned >= 4,
            $"deveriam ter chegado reforços enquanto o chefe vivia; chegaram {_ondas.ReinforcementsSpawned}");
        Verificar(_reforcosQuandoOChefeCaiu >= 0 && _ondas.ReinforcementsSpawned == _reforcosQuandoOChefeCaiu,
            $"depois do chefe cair não deveriam chegar mais reforços; eram {_reforcosQuandoOChefeCaiu}, terminou com {_ondas.ReinforcementsSpawned}");

        Concluir();
    }

    private static T? Encontrar<T>(Node no) where T : Node
    {
        if (no is T achado)
            return achado;

        foreach (var filho in no.GetChildren())
        {
            var dentro = Encontrar<T>(filho);
            if (dentro is not null)
                return dentro;
        }

        return null;
    }

    private HashSet<string> Especies(int onda)
        => _especiesPorOnda.TryGetValue(onda, out var especies) ? especies : [];

    private void Procurar(Node no)
    {
        if (no is HordeGameMode m)
            _modo ??= m;

        if (no is WaveDirector w)
            _ondas ??= w;

        foreach (var filho in no.GetChildren())
            Procurar(filho);
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[partida] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        var perfilReal = ProjectSettings.GlobalizePath(CaminhoDoPerfil);
        if (File.Exists(perfilReal))
            File.Delete(perfilReal);

        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[partida] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[partida] todas as verificações passaram ({Scenario})");
        GetTree().Quit();
    }
}
