using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.UI.HUD;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o tingimento de elite e a barra própria do chefe na árvore de
/// nós real, sem teclado.
/// </summary>
/// <remarks>
/// Não há POCO nenhum novo neste ticket para testar em xUnit -- é tudo fiação
/// entre <see cref="EnemyDefinition"/>, material de malha e o HUD. O que este
/// probe cobre, especificamente: se uma elite realmente tinge a própria
/// malha, se esse tingimento SOBREVIVE ao flash de dano e à telegrafia de
/// ataque (os dois já disputavam <c>MeshInstance3D.MaterialOverride</c> antes
/// deste ticket -- sem <see cref="CharacterContext.BodyBaseMaterial"/>, o
/// primeiro golpe ou windup apagaria o tingimento para sempre), e se o chefe
/// aparece/some da barra própria no HUD ao nascer/morrer.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/EnemyVisibilityProbe.tscn
/// </code>
/// </remarks>
public sealed partial class EnemyVisibilityProbe : Node
{
    /// <summary>Cena com o jogador e o chão.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    /// <summary>Cena do inimigo a testar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string EnemyScenePath { get; set; } = "res://scenes/characters/EnemyGrunt.tscn";

    private static readonly Color CorDeTesteDaElite = new(0.1f, 0.9f, 0.2f);
    private static readonly Vector3 Longe = new(-25f, 0.1f, -25f);

    private readonly List<string> _falhas = [];
    private EnemyPool? _pool;
    private PackedScene? _cenaDoInimigo;
    private BossHealthBar? _barraDoChefe;

    private EnemyDefinition? _definicaoDaElite;
    private EnemyDefinition? _definicaoDoChefe;
    private CharacterController? _elite;
    private CharacterController? _chefe;
    private MeshInstance3D? _malhaDaElite;
    private WorldHealthBar? _barraDaElite;

    private enum Fase { AdquirirElite, EliteSobreviveAFlash, EliteSobreviveATelegrafia, ChefeAparece, ChefeMorreEEsconde }
    private Fase _fase = Fase.AdquirirElite;
    private int _quadroDaFase;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        _cenaDoInimigo = GD.Load<PackedScene>(EnemyScenePath);

        if (packed is null || _cenaDoInimigo is null)
        {
            GD.PrintErr($"[visibilidade] não consegui carregar {ScenePath} ou {EnemyScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());

        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");
        if (_pool is null)
        {
            GD.PrintErr("[visibilidade] FALHA: EnemyPool não resolveu.");
            GetTree().Quit(1);
            return;
        }

        // Definições construídas em runtime, não `.tres` novos: são fixtures
        // de teste (uma elite/chefe de mentira sobre o MESMO grunt), não
        // conteúdo real do roster -- criar `.tres` para isto seria
        // balanceamento que ninguém joga.
        _definicaoDaElite = new EnemyDefinition { IsElite = true, EliteTint = CorDeTesteDaElite };
        _definicaoDoChefe = new EnemyDefinition { IsBoss = true };

        _pool.Prewarm(_cenaDoInimigo, _definicaoDaElite, 1);
        _pool.Prewarm(_cenaDoInimigo, _definicaoDoChefe, 1);

        GD.Print("[visibilidade] pool pronto");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pool is null || !_pool.IsReady)
            return;

        if (_barraDoChefe is null)
        {
            ProcurarBarraDoChefe(GetTree().Root);
            return;
        }

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.AdquirirElite: TickAdquirirElite(); break;
            case Fase.EliteSobreviveAFlash: TickEliteSobreviveAFlash(); break;
            case Fase.EliteSobreviveATelegrafia: TickEliteSobreviveATelegrafia(); break;
            case Fase.ChefeAparece: TickChefeAparece(); break;
            case Fase.ChefeMorreEEsconde: TickChefeMorreEEsconde(); break;
            default: break;
        }
    }

    private void TickAdquirirElite()
    {
        if (_quadroDaFase == 1)
        {
            _elite = _pool!.Acquire(_definicaoDaElite!, Longe);
            _malhaDaElite = _elite?.GetNodeOrNull<MeshInstance3D>("Corpo");
            _barraDaElite = _elite?.GetNodeOrNull<WorldHealthBar>("WorldHealthBar");

            if (_elite is null || _malhaDaElite is null || _barraDaElite is null)
            {
                Verificar(false, "Acquire da elite (ou a própria malha Corpo/WorldHealthBar) não resolveu.");
                Concluir();
                return;
            }

            return;
        }

        if (_quadroDaFase < 3)
            return;

        VerificarTingidaDeElite("logo após nascer");

        // Spec 11 §2.4 -- "barra maior, com nome", não só o tingimento da
        // própria malha: comunicar SÓ por cor violaria a própria regra de
        // daltonismo da spec 11 §4.
        Verificar(!_barraDaElite!.Scale.IsEqualApprox(Vector3.One),
            $"a barra de uma elite deveria crescer além do tamanho normal; escala={_barraDaElite.Scale}");

        var nome = _barraDaElite.GetNodeOrNull<Label3D>("Nome");
        Verificar(nome is not null && nome.Visible && nome.Text == "Grunt",
            $"a barra de uma elite deveria mostrar o nome do personagem (\"Grunt\"); nome={nome?.Text}, visível={nome?.Visible}");

        AvancarFase(Fase.EliteSobreviveAFlash);
    }

    private DamageFlashComponent? _flashDaElite;
    private bool _flashChegouAAcender;

    /// <remarks>
    /// Espera o SINAL de verdade (<c>IsFlashing</c>), não um número de
    /// quadros chutado: o dano é enfileirado, e quando exatamente
    /// <c>ResolveQueue</c> resolve isso depende da ordem entre este probe e o
    /// próprio inimigo na árvore naquele quadro -- a mesma defasagem que já
    /// exigiu cuidado em tickets anteriores. Duas janelas com teto (nunca
    /// espera para sempre): uma até o flash ACENDER, outra até ele APAGAR.
    /// </remarks>
    private void TickEliteSobreviveAFlash()
    {
        if (_quadroDaFase == 1)
        {
            _flashDaElite = _elite!.GetNodeOrNull<DamageFlashComponent>("DamageFlashComponent");

            // Dano direto, fora de combate de verdade: só precisa disparar
            // Health.Damaged, que é o que aciona o flash branco.
            _elite.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: 5f, Type: DamageType.Physical, HitPoint: _elite.GlobalPosition, Direction: Vector3.Forward,
                Knockback: 0f, SourceId: 0UL, SourceTag: "enemy_visibility_probe", IsCritical: false));
            return;
        }

        if (!_flashChegouAAcender)
        {
            if (_flashDaElite!.IsFlashing)
            {
                _flashChegouAAcender = true;

                var materialAgora = _malhaDaElite!.MaterialOverride as StandardMaterial3D;
                Verificar(materialAgora is not null && !materialAgora.Emission.IsEqualApprox(CorDeTesteDaElite),
                    "o flash de dano deveria estar visivelmente branco, sobrepondo o tingimento da elite agora");
            }
            else
            {
                Verificar(_quadroDaFase < 10, "o flash de dano deveria ter acendido por essa altura, e nunca acendeu.");
            }

            return;
        }

        if (_flashDaElite!.IsFlashing)
        {
            Verificar(_quadroDaFase < 30, "o flash de dano deveria ter apagado por essa altura, e continua aceso.");
            return;
        }

        VerificarTingidaDeElite("depois do flash de dano apagar");

        AvancarFase(Fase.EliteSobreviveATelegrafia);
    }

    private void TickEliteSobreviveATelegrafia()
    {
        if (_quadroDaFase == 1)
        {
            _elite!.Context!.AttackTelegraph!.LigarAviso();
            return;
        }

        if (_quadroDaFase == 3)
        {
            Verificar(_elite!.Context!.AttackTelegraph!.IsWarning, "a telegrafia deveria estar ligada agora");
            _elite.Context.AttackTelegraph.DesligarAviso();
            return;
        }

        if (_quadroDaFase < 6)
            return;

        VerificarTingidaDeElite("depois da telegrafia de ataque apagar");

        AvancarFase(Fase.ChefeAparece);
    }

    private void TickChefeAparece()
    {
        if (_quadroDaFase == 1)
        {
            _chefe = _pool!.Acquire(_definicaoDoChefe!, Longe + new Vector3(5f, 0f, 0f));

            Verificar(ReferenceEquals(ServiceLocator.Session.BossBody, _chefe),
                "adquirir um inimigo com IsBoss deveria anunciar em GameSession.BossBody na hora");
            return;
        }

        // Folga para o HudController (que roda de forma independente) notar
        // GameSession.BossBody e chamar Bind.
        if (_quadroDaFase < 6)
            return;

        Verificar(_barraDoChefe!.IsBound, "a barra do chefe deveria estar vinculada depois do HudController notar o BossBody");
        Verificar(_barraDoChefe.CurrentFraction > 0.99f, $"vida cheia deveria ser fração ~1, foi {_barraDoChefe.CurrentFraction}");

        AvancarFase(Fase.ChefeMorreEEsconde);
    }

    private void TickChefeMorreEEsconde()
    {
        if (_quadroDaFase == 1)
        {
            _chefe!.Context!.Health!.Kill("enemy_visibility_probe");
            return;
        }

        if (_quadroDaFase == 3)
        {
            Verificar(ServiceLocator.Session.BossBody is null,
                "o chefe morrer deveria limpar GameSession.BossBody na mesma hora (Died é síncrono)");
            return;
        }

        // Folga para o HudController notar que BossBody sumiu e desvincular.
        if (_quadroDaFase < 8)
            return;

        Verificar(!_barraDoChefe!.IsBound, "a barra do chefe deveria esconder de novo depois que ele morre");

        Concluir();
    }

    /// <summary>Confere que a malha da elite está tingida com a cor de teste, nem branca (flash) nem apagada (null).</summary>
    private void VerificarTingidaDeElite(string contexto)
    {
        var material = _malhaDaElite!.MaterialOverride as StandardMaterial3D;
        Verificar(material is not null && material.Emission.IsEqualApprox(CorDeTesteDaElite),
            $"[{contexto}] a malha da elite deveria continuar tingida com a cor de teste; material={material?.Emission}");
    }

    private void ProcurarBarraDoChefe(Node no)
    {
        if (no is BossHealthBar barra)
        {
            _barraDoChefe = barra;
            return;
        }

        // Mesma disciplina do HudProbe: varredura tolerada aqui porque só
        // roda por 1-2 quadros no boot, até o GameBootstrap montar o HUD.
        foreach (var filho in no.GetChildren())
        {
            ProcurarBarraDoChefe(filho);
            if (_barraDoChefe is not null)
                return;
        }
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[visibilidade] FALHA: {mensagem}");
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
            GD.PrintErr($"[visibilidade] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[visibilidade] todas as verificações passaram");
        GetTree().Quit();
    }
}
