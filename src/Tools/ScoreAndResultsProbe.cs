using System.Collections.Generic;
using System.IO;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.GameModes;
using Contenda.GameModes.Horde;
using Contenda.Persistence;
using Contenda.UI.HUD;
using Contenda.UI.Menus;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica a fiação do placar e do fim de partida na árvore real (ticket 29):
/// o combo sobe com abates seguidos e ZERA quando o jogador apanha, o HUD
/// reage na hora, a tela de resultado mostra o que aconteceu, o recorde vai
/// para o arquivo de perfil, e "tentar novamente" recarrega a arena.
/// </summary>
/// <remarks>
/// As contas do placar e o formato/atomicidade do perfil são xUnit
/// (<c>ScoreKeeperTests</c>, <c>ProfileStoreTests</c>) -- aqui só o que só
/// aparece com a árvore de nós: eventos de verdade, o HUD de verdade, um
/// `Button` de verdade. O perfil vai para `user://probe_profile.cfg`, nunca o
/// de verdade.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/ScoreAndResultsProbe.tscn
/// </code>
/// </remarks>
public sealed partial class ScoreAndResultsProbe : Node
{
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    private const string CaminhoDoPerfil = "user://probe_profile.cfg";

    // Sobrevive ao ReloadCurrentScene: é como a segunda instância do probe
    // sabe que "tentar novamente" recarregou a cena de verdade.
    private static bool s_recarregou;

    private enum Fase { Combo, ReiniciarCombo, Derrota, Recarregado }

    private readonly List<string> _falhas = [];
    private HordeGameMode? _modo;
    private EnemyPool? _pool;
    private CharacterController? _jogador;
    private ScoreDisplay? _placar;
    private ResultsScreen? _tela;

    private Fase _fase = Fase.Combo;
    private int _quadro;
    private int _quadroDaFase;
    private bool _comecou;

    private float _maiorCombo;
    private string _textoDoComboNoPico = "";
    private bool _danoAplicado;
    private bool _viuReset;
    private float _ultimoCombo = 1f;
    private GameModeResult _resultado;
    private bool _resultadoVisto;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[placar] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);
        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");
        _jogador = ServiceLocator.Session.PlayerBody;

        if (_modo is null || _pool is null || _jogador is null)
        {
            GD.PrintErr("[placar] FALHA: HordeGameMode, EnemyPool ou jogador não resolveram.");
            GetTree().Quit(1);
            return;
        }

        _modo.ProfilePath = CaminhoDoPerfil;

        if (s_recarregou)
        {
            _fase = Fase.Recarregado;
            return;
        }

        // Começa de um perfil limpo: o probe confere o que ELE gravou.
        var real = ProjectSettings.GlobalizePath(CaminhoDoPerfil);
        if (File.Exists(real))
            File.Delete(real);

        _modo.MatchEnded += resultado =>
        {
            _resultado = resultado;
            _resultadoVisto = true;
        };

        ServiceLocator.Events.ScoreChanged += AoMudarPlacar;

        GD.Print("[placar] cena pronta");
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.ScoreChanged -= AoMudarPlacar;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_modo is null || _pool is null || _jogador is null || !_pool.IsReady)
            return;

        _quadro++;
        _quadroDaFase++;

        // HUD e tela de resultado entram na árvore adiados pelo GameBootstrap.
        _placar ??= Encontrar<ScoreDisplay>(GetTree().Root);
        _tela ??= Encontrar<ResultsScreen>(GetTree().Root);
        if (_placar is null || _tela is null)
            return;

        if (_fase == Fase.Recarregado)
        {
            TickRecarregado();
            return;
        }

        if (!_comecou)
        {
            _comecou = true;
            _modo.StartMatch();
            return;
        }

        if (_modo.State != GameModeState.Playing && !_resultadoVisto)
            return;

        switch (_fase)
        {
            case Fase.Combo: TickCombo(); break;
            case Fase.ReiniciarCombo: TickReiniciarCombo(); break;
            case Fase.Derrota: TickDerrota(); break;
        }
    }

    // --- abates seguidos sobem o combo; o HUD mostra ---

    private void TickCombo()
    {
        Verificar(!_tela!.IsShowing, "a tela de resultado deveria estar escondida durante a partida");
        MatarTodosOsInimigos();

        if (_maiorCombo < 1.3f && _quadroDaFase < 60 * 30)
            return;

        Verificar(_maiorCombo >= 1.3f, $"abates em sequência rápida deveriam subir o combo; o maior foi x{_maiorCombo:0.0}");
        Verificar(_textoDoComboNoPico.StartsWith('x'), $"o HUD deveria mostrar o combo (\"xN.N\"); mostrou \"{_textoDoComboNoPico}\"");
        Verificar(_placar!.ScoreText.StartsWith("SCORE"), $"o HUD deveria mostrar o placar; mostrou \"{_placar.ScoreText}\"");

        // Apanhar: o jogador leva um golpe pequeno, sem morrer.
        _jogador!.Context!.Health!.ApplyDamage(new DamageInfo(
            Amount: 1f, Type: DamageType.Physical, HitPoint: _jogador.GlobalPosition,
            Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL, SourceTag: "placar_probe", IsCritical: false));
        _danoAplicado = true;
        Avancar(Fase.ReiniciarCombo);
    }

    // --- apanhar zera o multiplicador ---

    private void TickReiniciarCombo()
    {
        // Espera o aviso do dano E a invulnerabilidade pós-golpe acabar: o
        // golpe letal seguinte seria descartado dentro dela.
        if ((!_viuReset || _jogador!.Context!.Health!.IsInvulnerable) && _quadroDaFase < 120)
            return;

        // Abates ainda em voo podem chegar antes do aviso de dano; o que vale
        // é o multiplicador voltar a 1,0 logo depois de apanhar.
        Verificar(_viuReset, "apanhar deveria zerar o multiplicador (um ScoreChanged com x1,0 logo depois do dano)");

        // O HUD acompanha o último aviso: sem combo, sem texto de combo.
        Verificar((_ultimoCombo <= 1.0001f) == (_placar!.ComboText == ""),
            $"o HUD deveria refletir o último combo (x{_ultimoCombo:0.0}); mostra \"{_placar.ComboText}\"");

        // Mata o jogador: derrota.
        _jogador!.Context!.Health!.ApplyDamage(new DamageInfo(
            Amount: 99999f, Type: DamageType.Physical, HitPoint: _jogador.GlobalPosition,
            Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL, SourceTag: "placar_probe", IsCritical: false));
        Avancar(Fase.Derrota);
    }

    // --- tela de resultado, perfil, tentar de novo ---

    private void TickDerrota()
    {
        if (!_resultadoVisto && _quadroDaFase < 120)
            return;

        Verificar(_resultadoVisto, "morrer deveria encerrar a partida");
        if (!_resultadoVisto)
        {
            Concluir();
            return;
        }

        Verificar(_resultado.Score > 0, $"a partida deveria ter pontuado; pontuou {_resultado.Score}");

        Verificar(_tela!.IsShowing, "a tela de resultado deveria aparecer ao fim da partida");
        Verificar(_tela.TitleText == "DERROTA", $"título deveria ser DERROTA; é \"{_tela.TitleText}\"");
        Verificar(_tela.ScoreText.Contains(ScoreDisplay.FormatarPontos(_resultado.Score)),
            $"a tela deveria mostrar os pontos ({_resultado.Score}); mostra \"{_tela.ScoreText}\"");
        Verificar(_tela.WavesText.Contains(_resultado.WavesCleared.ToString()), $"ondas: \"{_tela.WavesText}\"");
        Verificar(_tela.KillsText.Contains(_resultado.EnemiesKilled.ToString()), $"abates: \"{_tela.KillsText}\"");
        Verificar(_tela.TimeText.Contains(':'), $"tempo deveria vir em m:ss; veio \"{_tela.TimeText}\"");
        Verificar(_tela.CharacterText.Contains(_resultado.CharacterId.ToString()), $"personagem: \"{_tela.CharacterText}\"");

        Verificar(_tela.RetryButton is { Disabled: false }, "\"tentar novamente\" deveria estar habilitado");
        Verificar(_tela.ChangeCharacterButton is not null && _tela.MainMenuButton is not null,
            "os botões de trocar personagem e de menu principal deveriam existir (visíveis)");

        var perfil = new ProfileStore(ProjectSettings.GlobalizePath(CaminhoDoPerfil)).Load();
        var id = _resultado.CharacterId.ToString();
        Verificar(perfil.TotalMatches == 1, $"o perfil deveria ter 1 partida; tem {perfil.TotalMatches}");
        Verificar(perfil.Best.TryGetValue(id, out var melhor) && melhor.Score == _resultado.Score,
            $"o recorde de {id} deveria ser {_resultado.Score}");
        Verificar(!File.Exists(ProjectSettings.GlobalizePath(CaminhoDoPerfil) + ".tmp"),
            "a gravação atômica não deveria deixar .tmp para trás");

        // A segunda instância não enxerga as falhas desta.
        if (_falhas.Count > 0)
        {
            Concluir();
            return;
        }

        // Tentar novamente: a cena recarrega, e o probe reaparece como segunda instância.
        s_recarregou = true;
        _tela.RetryButton!.EmitSignal(BaseButton.SignalName.Pressed);
        _fase = Fase.Recarregado;
    }

    private void TickRecarregado()
    {
        if (_quadroDaFase < 5)
            return;

        Verificar(!_tela!.IsShowing, "depois de tentar de novo, a tela de resultado deveria ter sumido");
        Verificar(_modo!.State == GameModeState.Starting, "a arena recarregada deveria estar num modo novo, ainda não iniciado");

        var real = ProjectSettings.GlobalizePath(CaminhoDoPerfil);
        if (File.Exists(real))
            File.Delete(real);

        s_recarregou = false;
        Concluir();
    }

    private void AoMudarPlacar(ScoreChangedEvent evento)
    {
        if (evento.ComboMultiplier > _maiorCombo)
        {
            _maiorCombo = evento.ComboMultiplier;
            _textoDoComboNoPico = _placar?.ComboText ?? "";
        }

        _ultimoCombo = evento.ComboMultiplier;

        if (_danoAplicado && evento.ComboMultiplier <= 1.0001f)
            _viuReset = true;
    }

    private void MatarTodosOsInimigos()
    {
        foreach (var no in GetTree().GetNodesInGroup("damageable"))
        {
            if (no is not CharacterController { Visible: true } c || c.Team != Team.Enemy
                || c.Context is not { Health: { IsAlive: true } vida, EnemyBrain: not null })
                continue;

            vida.ApplyDamage(new DamageInfo(
                Amount: 99999f, Type: DamageType.Physical, HitPoint: c.GlobalPosition,
                Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL, SourceTag: "placar_probe", IsCritical: false));
        }
    }

    private void Procurar(Node no)
    {
        if (no is HordeGameMode m)
            _modo ??= m;

        foreach (var filho in no.GetChildren())
            Procurar(filho);
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

    private void Avancar(Fase proxima)
    {
        _fase = proxima;
        _quadroDaFase = 0;
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[placar] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[placar] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[placar] todas as verificações passaram");
        GetTree().Quit();
    }
}
