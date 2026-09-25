using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.GameModes.Horde;
using Contenda.UI.HUD;
using Contenda.Vfx;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o laço central do modo horda na árvore de nós real: se os
/// spawns de uma onda saem espaçados no tempo, longe do jogador e fora do
/// frustum da câmera, se o marcador no chão precede o inimigo de verdade,
/// se o recém-nascido nasce invulnerável, se limpar a onda avança para a
/// próxima com um respiro e um banner, e se um inimigo nunca abatido não
/// trava a partida. Ticket 27, spec 10.
/// </summary>
/// <remarks>
/// Usa um <see cref="WaveSetDefinition"/> PRÓPRIO, montado em memória, não
/// `waveset_default.tres`: precisa de contagens pequenas e tempos curtos
/// para rodar num probe em segundos, não nos ~8-12 minutos de uma partida de
/// verdade -- mesmo motivo de <c>MovementProbe</c> usar uma
/// <c>MovementSettings</c> própria em vez da do jogador real.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/WaveDirectorProbe.tscn
/// </code>
/// </remarks>
public sealed partial class WaveDirectorProbe : Node
{
    /// <summary>Cena com a arena, o jogador, o SpawnDirector e o WaveDirector já ligados (ticket 27).</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    /// <summary>Cena do HUD, para verificar o banner de onda de verdade.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string HudScenePath { get; set; } = "res://scenes/ui/hud/Hud.tscn";

    [Export(PropertyHint.File, "*.tres")]
    public string GruntDefinitionPath { get; set; } = "res://data/enemies/grunt.tres";

    /// <summary>
    /// O conjunto de ondas de verdade -- só carregado e conferido, nunca
    /// simulado por inteiro (rodar até o fim levaria minutos, não segundos,
    /// mesmo motivo de <see cref="TickEspacamento"/> montar o próprio
    /// conjunto pequeno em memória). Ver <see cref="VerificarDadosDeVerdade"/>.
    /// </summary>
    [Export(PropertyHint.File, "*.tres")]
    public string WaveSetDefinitionPath { get; set; } = "res://data/waves/waveset_default.tres";

    private enum Fase { Espacamento, LimpezaEBanner, Travado, Concluido }

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private WaveDirector? _waveDirector;
    private SpawnDirector? _spawnDirector;
    private EnemyPool? _pool;
    private SpawnMarkerPool? _marcadores;
    private WaveBanner? _banner;
    private EnemyDefinition? _grunt;

    private Fase _fase = Fase.Espacamento;
    private int _quadroDaFase;

    // --- fase 1: espaçamento, marcador antes do inimigo, invulnerabilidade, distância/frustum ---
    private readonly HashSet<CharacterController> _gruntsConhecidos = [];
    private readonly List<int> _quadrosDeSpawn = [];
    private int _quadroEmQueMarcadorApareceu = -1;
    private int _picoDeMarcadoresAtivos;
    private bool _viuEnemySemInvulnerabilidade;
    private bool _viuSpawnForaDoIntervalo;

    // --- fase 2 ---
    private int _quadroDoUltimoAbate;
    private bool _bannerApareceu;
    private bool _bannerSumiuSozinho;
    private int _quadroDaTransicaoDeOnda = -1;

    // --- fase 3 ---
    private const float StuckFallbackDeTeste = 1f;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        var hudPacked = GD.Load<PackedScene>(HudScenePath);
        _grunt = GD.Load<EnemyDefinition>(GruntDefinitionPath);

        if (packed is null || hudPacked is null || _grunt is null)
        {
            GD.PrintErr($"[horda-diretor] não consegui carregar {ScenePath}, {HudScenePath} ou {GruntDefinitionPath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        AddChild(hudPacked.Instantiate());
        Procurar(this);

        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");
        _marcadores = GetNodeOrNull<SpawnMarkerPool>("/root/SpawnMarkerPool");

        if (_jogador is null || _waveDirector is null || _spawnDirector is null || _pool is null
            || _marcadores is null || _banner is null)
        {
            GD.PrintErr("[horda-diretor] FALHA: jogador, WaveDirector, SpawnDirector, EnemyPool, "
                + "SpawnMarkerPool ou WaveBanner não resolveram.");
            GetTree().Quit(1);
            return;
        }

        VerificarDadosDeVerdade();

        GD.Print("[horda-diretor] cena pronta");
    }

    /// <summary>
    /// "A composição e o ritmo da onda vêm de arquivo de dados" (ticket 27):
    /// carrega <see cref="WaveSetDefinitionPath"/> de verdade e confere a
    /// FORMA (ondas, entradas, contagens), sem rodar a simulação inteira --
    /// é o que prova que o `.tres` de produção é válido e teria efeito real
    /// se trocado, sem gastar minutos simulando as ondas cheias aqui.
    /// </summary>
    private void VerificarDadosDeVerdade()
    {
        var conjunto = GD.Load<WaveSetDefinition>(WaveSetDefinitionPath);
        if (conjunto is null)
        {
            Verificar(false, $"{WaveSetDefinitionPath} deveria ter carregado como WaveSetDefinition");
            return;
        }

        Verificar(conjunto.Waves.Length > 0, $"{WaveSetDefinitionPath} deveria ter pelo menos uma onda");

        foreach (var onda in conjunto.Waves)
        {
            Verificar(!string.IsNullOrEmpty(onda.DisplayName), "toda onda deveria ter um DisplayName para o banner");
            Verificar(onda.Entries.Length > 0, $"a onda \"{onda.DisplayName}\" deveria ter pelo menos uma entrada de spawn");

            foreach (var entrada in onda.Entries)
            {
                Verificar(entrada.Enemy is not null, $"toda entrada da onda \"{onda.DisplayName}\" deveria referenciar um EnemyDefinition");
                Verificar(entrada.Count > 0, $"toda entrada da onda \"{onda.DisplayName}\" deveria pedir pelo menos 1 inimigo");
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_pool!.IsReady)
            return;

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.Espacamento: TickEspacamento(); break;
            case Fase.LimpezaEBanner: TickLimpezaEBanner(); break;
            case Fase.Travado: TickTravado(); break;
        }
    }

    // --- fase 1 ---

    private void TickEspacamento()
    {
        if (_quadroDaFase == 1)
        {
            var conjunto = new WaveSetDefinition
            {
                DisplayName = "Teste",
                Waves =
                [
                    new WaveDefinition
                    {
                        DisplayName = "ONDA 1",
                        Entries = [new EnemySpawnEntry { Enemy = _grunt, Count = 3 }],
                        SpawnInterval = 0.3f,
                        MaxConcurrent = 10,
                        // Maior que a duração cheia do banner (VisibleDuration
                        // + FadeDuration, 2,1 s por padrão) -- senão o
                        // anúncio da ONDA 2 reacende o banner antes dele
                        // terminar de sumir sozinho da ONDA 1, e "sumiu
                        // sozinho" nunca chega a ser observado.
                        CompletionDelay = 3f,
                    },
                    new WaveDefinition
                    {
                        DisplayName = "ONDA 2",
                        Entries = [new EnemySpawnEntry { Enemy = _grunt, Count = 1 }],
                        SpawnInterval = 0.3f,
                        MaxConcurrent = 10,
                        CompletionDelay = 3f,
                    },
                ],
            };

            _waveDirector!.StuckFallbackSeconds = StuckFallbackDeTeste;
            _waveDirector.Begin(conjunto);
            return;
        }

        if (_marcadores!.ActiveCount > 0)
            _quadroEmQueMarcadorApareceu = _quadroDaFase;

        _picoDeMarcadoresAtivos = Mathf.Max(_picoDeMarcadoresAtivos, _marcadores.ActiveCount);

        DetectarNovosSpawns();

        if (_quadroDaFase < 200)
            return;

        Verificar(_gruntsConhecidos.Count == 3,
            $"deveriam ter nascido 3 grunts na onda 1; nasceram {_gruntsConhecidos.Count}");

        // Espaçado no tempo: com SpawnInterval=0,3 s (18 quadros) para 3
        // spawns, o primeiro e o último não deveriam estar a menos de ~30
        // quadros um do outro -- bem menos que isso provaria "todos de uma
        // vez".
        if (_quadrosDeSpawn.Count >= 2)
        {
            var espalhamento = _quadrosDeSpawn[^1] - _quadrosDeSpawn[0];
            Verificar(espalhamento >= 30,
                $"os spawns da onda deveriam estar espaçados no tempo; primeiro e último saíram a só {espalhamento} quadros de diferença");
        }

        Verificar(_picoDeMarcadoresAtivos > 0, "o marcador no chão deveria ter aparecido antes de cada inimigo");
        Verificar(!_viuSpawnForaDoIntervalo,
            "todo spawn deveria estar entre MinDistanceFromPlayer e MaxDistanceFromPlayer do jogador, ou fora do frustum da câmera");
        Verificar(!_viuEnemySemInvulnerabilidade, "um inimigo recém-nascido deveria começar invulnerável");

        AvancarFase(Fase.LimpezaEBanner);
    }

    /// <summary>
    /// Compara os inimigos ativos agora com os já vistos POR IDENTIDADE (não
    /// por posição -- um grunt persegue, a própria posição muda todo quadro),
    /// e valida qualquer um novo.
    /// </summary>
    /// <remarks>
    /// Pelo grupo "damageable", não <see cref="EnemyPool.ObterPosicoesAtivas"/>:
    /// aquele método devolve só posição de propósito (a força de separação
    /// não precisa de mais que isso), mas este probe também quer conferir
    /// <see cref="HealthComponent.IsInvulnerable"/>, que exige o
    /// <see cref="CharacterController"/> inteiro.
    ///
    /// <c>Context.EnemyBrain is not null</c> filtra o grupo, obrigatório, não
    /// só uma otimização: Arena.tscn já tem `Manequim1/2/3` (bonecos de
    /// treino, ticket 08) no MESMO grupo "damageable", com o MESMO
    /// `Team.Enemy` -- sem este filtro, a física de assentamento deles logo
    /// no início (a posição muda um pouco a cada quadro por alguns quadros)
    /// seria lida como dezenas de "novos spawns" só de ruído. Um boneco de
    /// treino nunca tem `EnemyBrain` (não pensa, não persegue); um grunt
    /// nascido pelo <see cref="SpawnDirector"/> sempre tem.
    ///
    /// <c>c.Visible</c> filtra os OUTROS ~57 grunts do prewarm do
    /// <see cref="EnemyPool"/> (ticket 25): eles têm `EnemyBrain`, mas
    /// ficam desativados (`Visible = false`, entre outras coisas, pelo
    /// contrato de reciclagem, spec 09 §8) até um `Acquire` de verdade -- sem
    /// isto, o estoque inteiro pareceria "ativo" desde o boot.
    /// </remarks>
    private void DetectarNovosSpawns()
    {
        foreach (var no in GetTree().GetNodesInGroup("damageable"))
        {
            if (GruntDaOndaOuNulo(no) is not { } c || c.Context is not { Health: { } vida })
                continue;

            if (!_gruntsConhecidos.Add(c))
                continue; // já contado num quadro anterior -- só a POSIÇÃO muda, ele persegue

            var pos = c.GlobalPosition;
            _quadrosDeSpawn.Add(_quadroDaFase);

            // Marcador precede o inimigo: em algum quadro ANTES deste um
            // marcador esteve ativo. Não exige "no quadro estritamente
            // anterior, nunca no mesmo" -- SpawnDirector conta o próprio
            // TelegraphSeconds e SpawnMarkerPool conta a MESMA duração
            // independentemente, e qual dos dois `_PhysicsProcess` roda
            // primeiro no quadro exato em que os dois relógios zeram juntos
            // não é (nem precisa ser) garantido -- policiar essa borda
            // testaria a ordem de processamento de nós, não o comportamento.
            if (_quadroEmQueMarcadorApareceu <= 0)
                Verificar(false, $"o marcador deveria ter aparecido antes do spawn em {pos}, mas nunca apareceu");

            var distancia = pos.DistanceTo(_jogador!.GlobalPosition);
            var dentroDoIntervalo = distancia >= _spawnDirector!.MinDistanceFromPlayer
                && distancia <= _spawnDirector.MaxDistanceFromPlayer;
            if (!dentroDoIntervalo)
                _viuSpawnForaDoIntervalo = true;

            if (!vida.IsInvulnerable)
                _viuEnemySemInvulnerabilidade = true;
        }
    }

    // --- fase 2: limpar a onda avança com respiro; o banner aparece e some sozinho ---

    /// <remarks>
    /// O banner da ONDA 1 já apareceu e sumiu inteiro lá na Fase.Espacamento
    /// (bem antes desta fase começar) -- o que esta fase observa é o banner
    /// da ONDA 2, que só acende no EXATO quadro em que
    /// <c>CurrentWaveIndex</c> vira 1. Por isso a verificação final não roda
    /// no mesmo quadro da transição (o banner mal teria acendido ainda):
    /// espera <see cref="_quadroDaTransicaoDeOnda"/> mais uma folga
    /// (VisibleDuration + FadeDuration do banner, ~2,1 s por padrão) antes
    /// de checar se ele já sumiu sozinho.
    /// </remarks>
    private void TickLimpezaEBanner()
    {
        if (_quadroDaFase == 1)
        {
            MatarTodosOsAtivos();
            _quadroDoUltimoAbate = 0;
            return;
        }

        if (_banner!.IsShowing)
            _bannerApareceu = true;
        else if (_bannerApareceu)
            _bannerSumiuSozinho = true;

        if (_waveDirector!.CurrentWaveIndex == 0)
        {
            _quadroDoUltimoAbate++;
            return;
        }

        if (_quadroDaTransicaoDeOnda < 0)
            _quadroDaTransicaoDeOnda = _quadroDaFase;

        // Folga generosa além do ciclo cheio do banner (VisibleDuration +
        // FadeDuration, padrão 2,1 s = ~126 quadros) para dar tempo dele
        // aparecer E sumir sozinho depois da transição.
        if (_quadroDaFase - _quadroDaTransicaoDeOnda < 180)
            return;

        var respiroEmQuadros = _quadroDoUltimoAbate;
        var respiroEsperadoEmQuadros = 3f * 60f; // CompletionDelay desta onda, a 60 Hz

        Verificar(_waveDirector.CurrentWaveIndex == 1,
            $"deveria ter avançado para a onda 2; está na {_waveDirector.CurrentWaveIndex}");
        Verificar(respiroEmQuadros >= respiroEsperadoEmQuadros * 0.5f,
            $"deveria ter esperado um respiro antes da próxima onda; avançou em só {respiroEmQuadros} quadros");
        Verificar(_bannerApareceu, "o banner da onda deveria ter aparecido");
        Verificar(_bannerSumiuSozinho, "o banner deveria ter sumido sozinho, sem ninguém mandar");

        AvancarFase(Fase.Travado);
    }

    private void MatarTodosOsAtivos()
    {
        // Grupo "damageable": o mesmo caminho que qualquer arma usa para
        // atingir todo mundo numa área, sem precisar de referência nenhuma
        // guardada de antes. GruntDaOndaOuNulo: só os grunts desta onda,
        // nunca os bonecos de treino da arena (ver DetectarNovosSpawns).
        foreach (var no in GetTree().GetNodesInGroup("damageable"))
        {
            if (GruntDaOndaOuNulo(no) is not { } c || c.Context is not { Health: { IsAlive: true } vida })
                continue;

            vida.ApplyDamage(new DamageInfo(
                Amount: 9999f, Type: DamageType.Physical, HitPoint: c.GlobalPosition,
                Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL, SourceTag: "wave_probe", IsCritical: false));
        }
    }

    /// <summary>
    /// O filtro compartilhado por <see cref="DetectarNovosSpawns"/> e
    /// <see cref="MatarTodosOsAtivos"/>: um grunt de verdade, nascido pelo
    /// <see cref="SpawnDirector"/> desta onda -- nunca um boneco de treino
    /// (sem <c>EnemyBrain</c>) nem um grunt ainda no estoque do
    /// <see cref="EnemyPool"/> (<c>Visible = false</c> até um `Acquire`).
    /// </summary>
    private static CharacterController? GruntDaOndaOuNulo(Node no) =>
        no is CharacterController { Visible: true } c && c.Team == Team.Enemy && c.Context?.EnemyBrain is not null
            ? c
            : null;

    // --- fase 3: um inimigo nunca abatido não trava a partida ---

    /// <remarks>
    /// "Preso no cenário", pela spec 10 §4, é uma DISCORDÂNCIA -- este
    /// bookkeeping dizendo <c>EnemiesRemaining == 0</c> enquanto o pool
    /// ainda mostra alguém ativo -- não simplesmente "um inimigo vivo que
    /// ninguém matou ainda" (isso é o funcionamento NORMAL de esperar a
    /// onda, não travamento nenhum: se qualquer inimigo vivo forçasse
    /// avanço, a própria regra "matar todos avança a onda" deixaria de
    /// existir). Por isso este teste não simplesmente "nunca mata" o grunt
    /// da onda 2 -- ele dispara <see cref="GameEvents.EnemyKilled"/> na mão,
    /// sem matar ninguém de verdade, para produzir a MESMA discordância que
    /// um abate perdido (bug) produziria, e prova que o alçapão de
    /// <see cref="StuckFallbackDeTeste"/> reage a ela.
    /// </remarks>
    private void TickTravado()
    {
        if (_quadroDaFase < 90)
            return; // dá tempo do único grunt da onda 2 nascer (protegido/invulnerável, não importa: não vou matá-lo)

        if (_quadroDaFase == 90)
        {
            ServiceLocator.Events.RaiseEnemyKilled(new EnemyKilledEvent(false, 0));
            return;
        }

        // Depois do alçapão forçar Respiro, o respiro em si (CompletionDelay
        // desta onda, 3 s = 180 quadros) ainda roda por inteiro -- o
        // alçapão pula a ESPERA por abates, não a pausa festiva de sempre.
        if (_quadroDaFase < 90 + (int)(StuckFallbackDeTeste * 60f) + 180 + 30)
            return;

        Verificar(_waveDirector!.CurrentWave is null,
            "um abate perdido não deveria travar a partida; a onda (a última) deveria ter concluído mesmo com o pool ainda ativo");
        Verificar(_pool!.ActiveCount > 0,
            "o grunt \"preso\" deveria continuar ativo -- o alçapão avança a onda, não mata ninguém à força");

        Concluir();
    }

    private void Procurar(Node no)
    {
        if (no is CharacterController c && c.Team == Team.Player && c.Context?.Combat is not null)
            _jogador ??= c;

        if (no is WaveDirector wd)
            _waveDirector ??= wd;

        if (no is SpawnDirector sd)
            _spawnDirector ??= sd;

        if (no is WaveBanner wb)
            _banner ??= wb;

        foreach (var filho in no.GetChildren())
            Procurar(filho);
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[horda-diretor] FALHA: {mensagem}");
    }

    private void AvancarFase(Fase proxima)
    {
        _fase = proxima;
        _quadroDaFase = 0;
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[horda-diretor] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[horda-diretor] todas as verificações passaram");
        GetTree().Quit();
    }
}
