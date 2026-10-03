using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Components.Health;
using Contenda.Core;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica que cada espécie nova do roster (ticket 28) tem o comportamento
/// que a distingue, na árvore de nós real: o <c>runner</c> é mais rápido, o
/// <c>shooter</c> machuca de longe, o <c>brute</c> bate forte, e o
/// <c>warlord</c> encadeia os três golpes do combo e se anuncia como chefe.
/// </summary>
/// <remarks>
/// Uma espécie por vez, todas contra o mesmo jogador parado numa posição
/// conhecida -- o que cada uma faz DE DIFERENTE é o que se mede, não o
/// laço de onda (esse é o <c>HordeMatchProbe</c>).
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/EnemyRosterProbe.tscn
/// </code>
/// </remarks>
public sealed partial class EnemyRosterProbe : Node
{
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    // Mesma geometria do EnemyBrainProbe: jogador no fim da rampa sul, o
    // inimigo no centro da praça -- linha de visão livre, ~15 m.
    private static readonly Vector3 PosicaoDoJogador = new(0f, 0.1f, 15f);
    private static readonly Vector3 CentroDaPraca = new(0f, 0.1f, 0f);
    private static readonly Vector3 LadoDoCentro = new(3f, 0.1f, 0f);
    private static readonly Vector3 PertoDoJogador = new(0f, 0.1f, 11f);

    private enum Fase { Velocidade, Atirador, Bruto, Chefe, Concluido }

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private EnemyPool? _pool;

    private EnemyDefinition? _grunt;
    private EnemyDefinition? _runner;
    private EnemyDefinition? _shooter;
    private EnemyDefinition? _brute;
    private EnemyDefinition? _warlord;

    private Fase _fase = Fase.Velocidade;
    private int _quadroDaFase;

    private CharacterController? _a;
    private CharacterController? _b;
    private float _picoA;
    private float _picoB;

    private float _vidaAntes;
    private float _maiorDanoDeUmGolpe;
    private float _distanciaNoPrimeiroDano = -1f;
    private int _maiorPassoDeCombo;
    private bool _chefeSeAnunciou;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[roster] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        _jogador = ServiceLocator.Session.PlayerBody;
        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");

        _grunt = GD.Load<EnemyDefinition>("res://data/enemies/grunt.tres");
        _runner = GD.Load<EnemyDefinition>("res://data/enemies/runner.tres");
        _shooter = GD.Load<EnemyDefinition>("res://data/enemies/shooter.tres");
        _brute = GD.Load<EnemyDefinition>("res://data/enemies/brute.tres");
        _warlord = GD.Load<EnemyDefinition>("res://data/enemies/warlord.tres");

        if (_jogador is null || _pool is null || _grunt is null || _runner is null
            || _shooter is null || _brute is null || _warlord is null)
        {
            GD.PrintErr("[roster] FALHA: jogador, EnemyPool ou alguma EnemyDefinition não resolveram.");
            GetTree().Quit(1);
            return;
        }

        foreach (var definicao in new[] { _runner, _shooter, _brute, _warlord })
        {
            if (definicao.Scene is null)
            {
                GD.PrintErr("[roster] FALHA: uma EnemyDefinition do roster não tem Scene.");
                GetTree().Quit(1);
                return;
            }

            _pool.Prewarm(definicao.Scene, definicao, 2);
        }

        GD.Print("[roster] cena pronta");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pool is null || !_pool.IsReady || _jogador is null)
            return;

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.Velocidade: TickVelocidade(); break;
            case Fase.Atirador: TickAtirador(); break;
            case Fase.Bruto: TickBruto(); break;
            case Fase.Chefe: TickChefe(); break;
        }
    }

    // --- runner mais rápido que grunt ---

    private void TickVelocidade()
    {
        if (_quadroDaFase == 1)
        {
            Posicionar(_jogador!, PosicaoDoJogador);
            _a = _pool!.Acquire(_runner!, CentroDaPraca);
            _b = _pool.Acquire(_grunt!, LadoDoCentro);
            return;
        }

        if (_a is null || _b is null)
        {
            Verificar(false, "Acquire devolveu null para o runner ou o grunt.");
            Avancar(Fase.Concluido);
            return;
        }

        VerificarApresentacao(_a, "runner");
        VerificarApresentacao(_b, "grunt");

        _picoA = Mathf.Max(_picoA, VelocidadeHorizontal(_a));
        _picoB = Mathf.Max(_picoB, VelocidadeHorizontal(_b));

        if (_quadroDaFase < 150)
        {
            if (_quadroDaFase == 10)
            {
                VerificarAlertaVisual(_a, "runner");
                VerificarAlertaVisual(_b, "grunt");
            }
            return;
        }

        Verificar(_picoA > _picoB + 1.0f,
            $"o runner deveria correr bem mais que o grunt; pico do runner {_picoA:0.0} m/s, do grunt {_picoB:0.0} m/s");
        Verificar(_picoA > 4.5f, $"o runner deveria passar de 4,5 m/s; chegou a {_picoA:0.0}");

        Matar(_a);
        Matar(_b);
        Avancar(Fase.Atirador);
    }

    // --- shooter machuca de longe ---

    private void TickAtirador()
    {
        var vida = _jogador!.Context!.Health!;

        if (_quadroDaFase == 1)
        {
            vida.Heal(9999f);
            Posicionar(_jogador, PosicaoDoJogador);
            _a = _pool!.Acquire(_shooter!, CentroDaPraca);
            _vidaAntes = vida.Current;
            _distanciaNoPrimeiroDano = -1f;
            return;
        }

        if (_quadroDaFase == 10 && _a is not null)
            VerificarAlertaVisual(_a, "shooter");

        if (_a is null)
        {
            Verificar(false, "Acquire devolveu null para o shooter.");
            Avancar(Fase.Concluido);
            return;
        }

        VerificarApresentacao(_a, "shooter");

        if (_distanciaNoPrimeiroDano < 0f && vida.Current < _vidaAntes)
            _distanciaNoPrimeiroDano = _a.GlobalPosition.DistanceTo(_jogador.GlobalPosition);

        // Termina cedo assim que o primeiro tiro pega; senão, até o limite.
        if (_distanciaNoPrimeiroDano < 0f && _quadroDaFase < 1200)
            return;

        Verificar(_distanciaNoPrimeiroDano >= 0f, "o shooter deveria ter machucado o jogador de longe");
        Verificar(_distanciaNoPrimeiroDano > 6f,
            $"o primeiro tiro deveria vir de longe (> 6 m), veio de {_distanciaNoPrimeiroDano:0.0} m");

        Matar(_a);
        Avancar(Fase.Bruto);
    }

    // --- brute bate forte ---

    private void TickBruto()
    {
        var vida = _jogador!.Context!.Health!;

        if (_quadroDaFase == 1)
        {
            vida.Heal(9999f);
            Posicionar(_jogador, PosicaoDoJogador);
            _a = _pool!.Acquire(_brute!, PertoDoJogador);
            _vidaAntes = vida.Current;
            _maiorDanoDeUmGolpe = 0f;
            return;
        }

        if (_quadroDaFase == 10 && _a is not null)
            VerificarAlertaVisual(_a, "brute");

        if (_a is null)
        {
            Verificar(false, "Acquire devolveu null para o brute.");
            Avancar(Fase.Concluido);
            return;
        }

        VerificarApresentacao(_a, "brute");

        var perdido = _vidaAntes - vida.Current;
        if (perdido > 0f)
            _maiorDanoDeUmGolpe = Mathf.Max(_maiorDanoDeUmGolpe, perdido);

        if (_maiorDanoDeUmGolpe <= 0f && _quadroDaFase < 480)
            return;

        Verificar(_brute!.IsElite, "o brute deveria ser uma elite");
        Verificar(_a.Context?.Health?.Max >= 150f, "o brute deveria ser um tanque (>= 150 de vida)");
        Verificar(_maiorDanoDeUmGolpe >= 15f,
            $"o brute deveria bater forte; o primeiro golpe tirou {_maiorDanoDeUmGolpe:0.0}");

        Matar(_a);
        Avancar(Fase.Chefe);
    }

    // --- warlord encadeia o combo e se anuncia ---

    private void TickChefe()
    {
        var vida = _jogador!.Context!.Health!;

        if (_quadroDaFase == 1)
        {
            vida.Heal(9999f);
            vida.GrantInvulnerability(999f);
            Posicionar(_jogador, PosicaoDoJogador);
            _a = _pool!.Acquire(_warlord!, PertoDoJogador);
            _maiorPassoDeCombo = 0;
            _chefeSeAnunciou = false;

            if (_a?.Context?.Combat is { } combate)
                combate.AttackStarted += passo => _maiorPassoDeCombo = Math.Max(_maiorPassoDeCombo, passo);

            return;
        }

        if (_quadroDaFase == 10 && _a is not null)
            VerificarAlertaVisual(_a, "warlord");

        if (_a is null)
        {
            Verificar(false, "Acquire devolveu null para o warlord.");
            Avancar(Fase.Concluido);
            return;
        }

        VerificarApresentacao(_a, "warlord");

        if (ReferenceEquals(ServiceLocator.Session.BossBody, _a))
            _chefeSeAnunciou = true;

        if (_maiorPassoDeCombo < 3 && _quadroDaFase < 720)
            return;

        Verificar(_chefeSeAnunciou, "o warlord deveria se anunciar como chefe (GameSession.BossBody)");
        Verificar(_maiorPassoDeCombo >= 3,
            $"o warlord deveria encadear 3 golpes num ataque; chegou ao passo {_maiorPassoDeCombo}");
        Verificar(_a.Context?.Health?.Max >= 800f, "o warlord deveria ter muita vida (>= 800)");

        Matar(_a);
        Avancar(Fase.Concluido);
    }

    private static float VelocidadeHorizontal(CharacterController c)
        => new Vector2(c.Velocity.X, c.Velocity.Z).Length();

    private void VerificarApresentacao(CharacterController? inimigo, string idEsperado)
    {
        if (inimigo?.Definition is not { } definicao
            || inimigo.CurrentModel is null
            || inimigo.CurrentSkeleton is null
            || inimigo.CurrentBodyMesh is null
            || inimigo.Context?.Animator is not { Tree: { Active: true }, AnimationPlayer: { } player }
            || definicao.AnimationSet is not { } animationSet)
        {
            Verificar(false, $"{idEsperado}: modelo, rig ou AnimationTree não foram montados.");
            return;
        }

        Verificar(definicao.Id.ToString() == idEsperado, $"esperava definição visual {idEsperado}, recebeu {definicao.Id}.");
        var modeloEsperado = idEsperado switch
        {
            "grunt" => "enemy_mage.glb",
            "runner" => "enemy_rogue_hooded.glb",
            "shooter" => "enemy_ranger.glb",
            "brute" => "enemy_barbarian.glb",
            "warlord" => "enemy_knight.glb",
            _ => string.Empty
        };
        Verificar(definicao.ModelScene?.ResourcePath.EndsWith(modeloEsperado, StringComparison.Ordinal) == true,
            $"{idEsperado}: ModelScene não aponta para a silhueta KayKit esperada ({modeloEsperado}).");
        Verificar(player.HasAnimation($"motion/{animationSet.Idle}"),
            $"{idEsperado}: animação de repouso não foi carregada no AnimationPlayer.");
        Verificar(player.HasAnimation($"motion/{animationSet.Walk}"),
            $"{idEsperado}: animação de caminhada não foi carregada no AnimationPlayer.");
        Verificar(player.HasAnimation($"motion/{animationSet.Run}"),
            $"{idEsperado}: animação de corrida não foi carregada no AnimationPlayer.");
        Verificar(!animationSet.Alert.IsEmpty && player.HasAnimation($"motion/{animationSet.Alert}"),
            $"{idEsperado}: animação de alerta não foi carregada no AnimationPlayer.");
        Verificar(player.HasAnimation($"motion/{animationSet.Hit}"),
            $"{idEsperado}: animação de dano não foi carregada no AnimationPlayer.");
        Verificar(player.HasAnimation($"motion/{animationSet.Death}"),
            $"{idEsperado}: animação de morte não foi carregada no AnimationPlayer.");
        var ataqueEsperado = animationSet.MeleeAttacks.Length > 0
            ? animationSet.MeleeAttacks[0]
            : animationSet.Shoot;
        Verificar(!ataqueEsperado.IsEmpty && player.HasAnimation($"motion/{ataqueEsperado}"),
            $"{idEsperado}: animação de ataque não foi carregada no AnimationPlayer.");
        foreach (var attack in animationSet.MeleeAttacks)
            Verificar(player.HasAnimation($"motion/{attack}"),
                $"{idEsperado}: variação de ataque {attack} não foi carregada no AnimationPlayer.");

        var definicaoInimigo = inimigo.Context?.EnemyBrain?.Definition;
        var esperaCoroa = definicaoInimigo is { IsElite: true } or { IsBoss: true };
        var coroa = inimigo.CurrentModel!.FindChild("EliteSilhouette", true, false) as Node3D;
        Verificar((coroa?.Visible ?? false) == esperaCoroa,
            $"{idEsperado}: coroa geométrica deveria {(esperaCoroa ? "estar visível" : "estar ausente")}.");
        Verificar(!esperaCoroa || coroa?.GetChildCount() == 3,
            $"{idEsperado}: elite/chefe precisa de três espigões para leitura sem cor.");
    }

    private void VerificarAlertaVisual(CharacterController? inimigo, string idEsperado)
    {
        var animator = inimigo?.Context?.Animator;
        var estado = inimigo?.Context?.EnemyBrain?.Estado;
        var emAlerta = estado == EnemyState.Alert;
        var active = animator?.Tree?.Get("parameters/Alert/active").AsBool() == true;
        var currentAnimation = animator?.AnimationPlayer?.CurrentAnimation.ToString() ?? "";
        var alertaTocando = active || currentAnimation == $"motion/{inimigo?.Definition?.AnimationSet?.Alert}";
        Verificar(emAlerta && alertaTocando,
            $"{idEsperado}: ao detectar o jogador, a animação de alerta deveria tocar junto ao estado Alert (estado={estado}, ativo={active}, clipe={currentAnimation}).");
    }

    private static void Posicionar(CharacterController quem, Vector3 posicao)
    {
        quem.Velocity = Vector3.Zero;
        quem.GlobalPosition = posicao;
    }

    private static void Matar(CharacterController c)
    {
        c.Context?.Health?.ApplyDamage(new DamageInfo(
            Amount: 99999f, Type: DamageType.Physical, HitPoint: c.GlobalPosition,
            Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL, SourceTag: "roster_probe", IsCritical: false));
    }

    private void Avancar(Fase proxima)
    {
        _fase = proxima;
        _quadroDaFase = 0;

        if (proxima == Fase.Concluido)
            Concluir();
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[roster] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[roster] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[roster] todas as verificações passaram");
        GetTree().Quit();
    }
}
