using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Combat;
using Contenda.Components.Movement;
using Contenda.Core;
using Contenda.Weapons;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o combate aéreo do corpo a corpo na árvore de nós real, sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem <c>AerialJuggleState</c> e
/// <c>AerialCombatMath.DeveComecarMergulho</c> isolados. O que eles não
/// alcançam é a fiação real: se o `.tres` da espada carrega o combo aéreo, se
/// atacar no ar de fato sai mais fraco, se o impulso vertical chega nos dois
/// envolvidos, se a gravidade cai de verdade durante a janela, se segurar o
/// ataque depois do pico mergulha, e se o teto de juggle realmente desliga o
/// impulso no quinto acerto. Ticket 19, spec 16 §6.
///
/// O impulso vertical é verificado por COMPARAÇÃO entre duas rodadas do
/// MESMO golpe, sob as MESMAS condições — uma com o teto de juggle livre,
/// outra pré-esgotado (ver <see cref="VerificarImpulsoPorComparacao"/>) —
/// em vez de reproduzir a integração da gravidade à mão: ela muda de escala
/// com uma defasagem de um quadro (<see cref="MovementComponent.ExternalGravityScale"/>),
/// e uma projeção própria seria frágil demais para servir de referência. A
/// gravidade reduzida em si é verificada à parte, lendo a mesma property
/// direto, num quadro dentro da janela.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/AerialCombatProbe.tscn
/// </code>
/// </remarks>
public sealed partial class AerialCombatProbe : Node
{
    /// <summary>Cena com o jogador e o manequim.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    private const int QuadrosParaAterrissarNoAr = 6;
    private const int QuadrosParaResolverGolpe = 30;
    private const int QuadrosDeTimeoutDoMergulho = 180;

    /// <summary>
    /// Ponto de chão plano e livre na RuaLesteNorte (Arena.tscn, ticket 21) --
    /// longe da praça rebaixada (que cobre a origem do mundo, -1,0 m, não
    /// 0,0 m) e das quatro rampas, com folga de sobra para os testes deste
    /// probe caírem/derivarem alguns metros sem sair do chão plano nem
    /// encostar no prédio leste (x = 16).
    /// </summary>
    private static readonly Vector3 AreaAberta = new(13f, 0f, 6f);

    private enum Fase { Dados, SoloVsAereo, ImpulsoEGravidade, TetoDeJuggle, Mergulho }

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private CharacterController? _alvo;
    private WeaponDefinition? _espada;

    private Fase _fase = Fase.Dados;
    private int _quadroDaFase;

    private int _acertos;
    private int _acertosAntesDoGolpe;
    private float _danoSoloObservado;
    private float _velocidadeAlvoAntesDoAcerto;
    private float _velocidadeJogadorAntesDoAcerto;
    private float _deltaAlvoComJuggleLivre;
    private float _deltaJogadorComJuggleLivre;
    private float _deltaAlvoComJuggleEsgotado;
    private float _deltaJogadorComJuggleEsgotado;
    private int _quadroDoAcerto = -1;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[aereo] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        _espada = GD.Load<WeaponDefinition>("res://data/weapons/sword.tres");

        if (_jogador?.Context?.Combat is null || _jogador.Context.Movement is null
            || _alvo?.Context?.Health is null || _alvo.Context.Movement is null || _espada is null)
        {
            GD.PrintErr("[aereo] FALHA: jogador sem Combat/Movement, manequim sem Health/Movement, ou sword.tres não carregou.");
            GetTree().Quit(1);
            return;
        }

        _jogador.Context.Combat.HitLanded += _ =>
        {
            _acertos++;
            if (_quadroDoAcerto < 0)
                _quadroDoAcerto = _quadroDaFase;
        };

        Reposicionar();

        GD.Print("[aereo] jogador e manequim prontos");
    }

    public override void _PhysicsProcess(double delta)
    {
        var combate = _jogador?.Context?.Combat;
        if (combate is null)
            return;

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.Dados: TickDados(); break;
            case Fase.SoloVsAereo: TickSoloVsAereo(combate); break;
            case Fase.ImpulsoEGravidade: TickImpulsoEGravidade(combate, comContadorZerado: false); break;
            case Fase.TetoDeJuggle: TickImpulsoEGravidade(combate, comContadorZerado: true); break;
            case Fase.Mergulho: TickMergulho(combate); break;
            default: break;
        }
    }

    private void TickDados()
    {
        Verificar(_espada!.AerialComboSteps.Length > 0,
            "a espada deveria ter um combo aéreo definido");
        Verificar(_espada.ComboSteps.Length > _espada.AerialComboSteps.Length,
            "o combo aéreo deveria ser mais CURTO que o de solo");

        if (_espada.AerialComboSteps.Length > 0)
        {
            Verificar(_espada.AerialComboSteps[0].DamageMultiplier < _espada.ComboSteps[0].DamageMultiplier,
                "o primeiro golpe aéreo deveria ser mais FRACO que o primeiro de solo");
            Verificar(_espada.AerialComboSteps[0].HitWindowEnd < _espada.ComboSteps[0].HitWindowEnd,
                "o primeiro golpe aéreo deveria ser mais RÁPIDO (janela mais curta) que o de solo");
        }

        Verificar(_espada.AerialVerticalKnockback > 0f, "deveria haver impulso vertical aéreo configurado");
        Verificar(_espada.AerialGravityScale < 1f, "a gravidade aérea deveria ser reduzida");
        Verificar(_espada.DiveRadius > 0f && _espada.DiveDamage > 0f, "a estocada de queda deveria ter dano e raio configurados");

        AvancarFase(Fase.SoloVsAereo);
    }

    private void TickSoloVsAereo(CombatComponent combate)
    {
        if (combate.IsAttacking)
            Reposicionar();

        // Alguns quadros de folga antes do primeiro pedido: o personagem
        // acabou de entrar na árvore (Arena.tscn) e o `IsGrounded` cacheado
        // do MovementComponent só reflete o chão de verdade depois de pelo
        // menos um MoveAndSlide -- pedir o golpe cedo demais correria o
        // risco de pegar esse acomodamento inicial no meio. 10, não 5: a
        // arena urbana (ticket 21) tem bem mais colisores estáticos para o
        // servidor de física assentar no boot (ver o mesmo ajuste em
        // CriticalProbe).
        const int quadroDoPedido = 10;

        if (_quadroDaFase == quadroDoPedido)
        {
            Reposicionar();
            Verificar(_jogador!.Context!.Movement!.IsGrounded, "o jogador deveria estar no chão para o golpe de solo");
            _danoSoloObservado = _alvo!.Context!.Health!.Current;
            combate.RequestBasicAttack();
        }

        if (_quadroDaFase < quadroDoPedido + QuadrosParaResolverGolpe)
            return;

        _danoSoloObservado -= _alvo!.Context!.Health!.Current;
        Verificar(_danoSoloObservado > 0f, "o golpe de solo deveria ter causado dano");

        combate.ResetForSpawn();
        _alvo.Context.Health.Heal(999999f);
        AvancarFase(Fase.ImpulsoEGravidade);
    }

    /// <summary>
    /// Golpe aéreo único contra o manequim.
    /// </summary>
    /// <remarks>
    /// Reaproveitada duas vezes, com condições de partida IDÊNTICAS: uma com
    /// o contador de juggle do manequim zerado (impulso deveria aplicar) e
    /// outra com ele pré-esgotado via quatro chamadas diretas a
    /// <c>RegistrarAcertoAereo</c> (impulso NÃO deveria aplicar). A
    /// velocidade final de cada rodada é só GUARDADA aqui -- a comparação
    /// entre as duas (que isola o impulso da gravidade, sem precisar
    /// reproduzir a integração dela) acontece só depois que as duas já
    /// rodaram, em <see cref="VerificarImpulsoPorComparacao"/>.
    /// </remarks>
    private void TickImpulsoEGravidade(CombatComponent combate, bool comContadorZerado)
    {
        // Reaproveita a mesma correção da CombatProbe: sem mira de mouse de
        // verdade, o cursor fica no canto da tela em modo headless, e o
        // jogador gira sozinho para encará-lo a cada quadro assim que a
        // rotação destrava. Manter o manequim relativo à frente ATUAL --
        // não à posição fixa de quando ele foi teleportado -- é o que isola
        // o golpe aéreo dessa deriva de mira, em vez de errar por acaso.
        if (_quadroDaFase > 1)
            ReposicionarNoAr();

        if (_quadroDaFase == 1)
        {
            TeleportarNoAr(_jogador!, AreaAberta + new Vector3(0f, 8f, 0f));
            TeleportarNoAr(_alvo!, AreaAberta + new Vector3(0f, 8f, -1.2f));
            return;
        }

        // Espera os dois deixarem de estar apoiados -- teleportar não some
        // com o `IsGrounded` cacheado até o próximo Tick de movimento.
        if (_quadroDaFase == QuadrosParaAterrissarNoAr)
        {
            Verificar(!_jogador!.Context!.Movement!.IsGrounded, "o jogador deveria estar no ar antes do golpe aéreo");

            if (comContadorZerado)
            {
                // Esgota o teto de juggle do manequim direto pela API real de
                // produção (HealthComponent), sem precisar coreografar quatro
                // golpes de verdade em sequência -- os quatro primeiros já
                // estão cobertos, isolados, por AerialJuggleStateTests.
                for (var i = 0; i < 4; i++)
                    _alvo!.Context!.Health!.RegistrarAcertoAereo();
            }

            _acertosAntesDoGolpe = _acertos;
            _quadroDoAcerto = -1;
            combate.RequestBasicAttack();
            return;
        }

        if (_quadroDaFase <= QuadrosParaAterrissarNoAr)
            return;

        // Sem isto, um golpe que por algum motivo nunca conecta (cone
        // errado, alvo fora de alcance) trava o probe para sempre em vez de
        // reportar a falha -- `_quadroDoAcerto` nunca sairia de -1.
        if (_quadroDoAcerto < 0 && _quadroDaFase > QuadrosParaAterrissarNoAr + 40)
        {
            Verificar(false, "o golpe aéreo deveria ter conectado dentro do tempo esperado e não conectou");
            Concluir();
            return;
        }

        // Verificado direto, sem matemática de projeção: um par de quadros
        // depois do acerto já é tempo mais que suficiente para o valor
        // definido no FIM do Tick anterior (ver o comentário de
        // ExternalGravityScale sobre a defasagem de 1 quadro) estar valendo.
        if (_quadroDoAcerto >= 0 && _quadroDaFase == _quadroDoAcerto + 3 && !comContadorZerado)
        {
            Verificar(
                Mathf.IsEqualApprox(_jogador!.Context!.Movement!.ExternalGravityScale, _espada!.AerialGravityScale, 0.01f),
                "a gravidade do jogador deveria estar reduzida durante a janela de acerto aérea; está "
                + $"{_jogador.Context.Movement.ExternalGravityScale:0.00}, esperado {_espada.AerialGravityScale:0.00}");
        }

        // Captura a velocidade de quem vai levar o golpe a cada quadro ATÉ
        // conectar -- o último valor guardado antes de `_quadroDoAcerto`
        // deixar de ser -1 é exatamente o quadro anterior ao acerto. É a
        // mesma referência de "antes" para o alvo e para o atacante, mesmo
        // que cada um seja lido "depois" em quadros diferentes logo abaixo.
        if (_quadroDoAcerto < 0)
        {
            _velocidadeAlvoAntesDoAcerto = _alvo!.Context!.Movement!.Velocity.Y;
            _velocidadeJogadorAntesDoAcerto = _jogador!.Context!.Movement!.Velocity.Y;
        }

        // A repulsão do KnockbackState não é um empurrão instantâneo: ela
        // FICA sendo somada à velocidade, quadro a quadro, decaindo aos
        // poucos ao longo de KnockbackDuration (0,25 s) -- ver
        // KnockbackState.cs. Isso significa que o quadro exato em que se lê
        // "depois" importa: ler tarde demais soma a mesma repulsão em cima
        // de novo (quase sem decair ainda) e o valor observado dobra. Cada
        // lado é lido no PRIMEIRO quadro em que a PRÓPRIA repulsão dele
        // aparece, nunca depois:
        // - o ALVO é um CharacterController À PARTE, cujo Movement.Tick já
        //   roda no MESMO quadro em que o golpe conecta (mas depois do
        //   ResolverAcertos que aplicou o ApplyKnockback) -- aparece já no
        //   quadro seguinte ao do acerto.
        // - o ATACANTE se autoempurra de DENTRO do próprio Combat.Tick, que
        //   já roda DEPOIS do próprio Movement.Tick daquele quadro -- só
        //   aparece um quadro DEPOIS disso, dois quadros após o acerto.
        if (_quadroDoAcerto >= 0 && _quadroDaFase == _quadroDoAcerto + 1)
        {
            Verificar(_acertos > _acertosAntesDoGolpe, "o golpe aéreo deveria ter conectado");

            var deltaAlvo = _alvo!.Context!.Movement!.Velocity.Y - _velocidadeAlvoAntesDoAcerto;

            if (comContadorZerado)
                _deltaAlvoComJuggleEsgotado = deltaAlvo;
            else
                _deltaAlvoComJuggleLivre = deltaAlvo;
        }

        if (_quadroDoAcerto >= 0 && _quadroDaFase == _quadroDoAcerto + 2)
        {
            var deltaJogador = _jogador!.Context!.Movement!.Velocity.Y - _velocidadeJogadorAntesDoAcerto;

            if (comContadorZerado)
                _deltaJogadorComJuggleEsgotado = deltaJogador;
            else
                _deltaJogadorComJuggleLivre = deltaJogador;
        }

        // `_quadroDoAcerto` ainda -1 (não conectou) tem que continuar
        // esperando aqui, não cair para o fim da fase por acidente: com
        // -1 + 10 = 9, `_quadroDaFase < 9` vira falso assim que o contador
        // de quadros passa de 9 -- mesmo que o golpe nunca tenha conectado.
        if (_quadroDoAcerto < 0 || _quadroDaFase < _quadroDoAcerto + 10)
            return;

        combate.ResetForSpawn();
        _alvo!.Context!.Health!.Heal(999999f);
        TeleportarNoChao();

        if (comContadorZerado)
            VerificarImpulsoPorComparacao();

        AvancarFase(comContadorZerado ? Fase.Mergulho : Fase.TetoDeJuggle);
    }

    /// <summary>
    /// Compara o DELTA de velocidade (depois - antes do acerto) das duas
    /// rodadas de <see cref="TickImpulsoEGravidade"/> (juggle livre vs.
    /// esgotado) para isolar a contribuição do impulso vertical, sem
    /// precisar reproduzir a integração da gravidade (que muda de escala
    /// com uma defasagem de 1 quadro -- ver
    /// <see cref="MovementComponent.ExternalGravityScale"/> -- e tornaria
    /// qualquer projeção própria frágil). As duas rodadas partem de
    /// condições idênticas; a única variável entre elas é o impulso. Usar o
    /// DELTA de cada rodada (não a velocidade bruta final) também absorve
    /// qualquer diferença de quantos quadros cada rodada levou até o golpe
    /// conectar -- ambas caem sob a MESMA gravidade reduzida durante essa
    /// janela, então essa parcela se cancela na subtração de qualquer jeito.
    /// </summary>
    private void VerificarImpulsoPorComparacao()
    {
        var impulsoNoAlvo = _deltaAlvoComJuggleLivre - _deltaAlvoComJuggleEsgotado;
        var impulsoNoJogador = _deltaJogadorComJuggleLivre - _deltaJogadorComJuggleEsgotado;

        Verificar(Mathf.Abs(impulsoNoAlvo - _espada!.AerialVerticalKnockback) < 0.3f,
            $"o impulso vertical no alvo deveria ser ~{_espada.AerialVerticalKnockback:0.00} m/s; observado {impulsoNoAlvo:0.00} "
            + $"(delta livre {_deltaAlvoComJuggleLivre:0.00}, delta esgotado {_deltaAlvoComJuggleEsgotado:0.00})");
        Verificar(Mathf.Abs(impulsoNoJogador - _espada.AerialVerticalKnockback) < 0.3f,
            $"o impulso vertical em quem golpeou deveria ser ~{_espada.AerialVerticalKnockback:0.00} m/s; observado {impulsoNoJogador:0.00} "
            + $"(delta livre {_deltaJogadorComJuggleLivre:0.00}, delta esgotado {_deltaJogadorComJuggleEsgotado:0.00})");
    }

    private void TickMergulho(CombatComponent combate)
    {
        if (_quadroDaFase == 1)
        {
            // Altura baixa de propósito: o mergulho avança para a FRENTE
            // enquanto cai (a diagonal da spec 16 §6), então de uma altura
            // maior o ponto de pouso deriva mais longe do que o manequim,
            // parado no lugar, aguenta -- colocá-lo bem embaixo do início da
            // queda cobre a deriva inteira com folga, sem precisar prever a
            // física exatamente.
            TeleportarNoAr(_jogador!, AreaAberta + new Vector3(0f, 3f, 0f));
            _jogador!.Rotation = Vector3.Zero;
            TeleportarNoAr(_alvo!, AreaAberta + new Vector3(0f, 0.1f, 0f));
            return;
        }

        if (_quadroDaFase == QuadrosParaAterrissarNoAr)
        {
            Verificar(!_jogador!.Context!.Movement!.IsGrounded, "o jogador deveria estar no ar antes do mergulho");

            // Segurado, não pressionado-e-solto: o primeiro clique ainda
            // pode abrir um golpe aéreo normal (spec 16 §6 -- "encadeia
            // naturalmente"), e o mergulho só dispara depois que ESSE golpe
            // esgota a própria janela de combo sem um segundo clique, com o
            // botão ainda embaixo. Ver o comentário da classe MeleeWeapon.Tick.
            Godot.Input.ActionPress(InputActionNames.AttackBasic);
            return;
        }

        if (_quadroDaFase < QuadrosParaAterrissarNoAr)
            return;

        if (_jogador!.Context!.Movement!.IsGrounded)
        {
            Godot.Input.ActionRelease(InputActionNames.AttackBasic);

            Verificar(_alvo!.Context!.Health!.Current < _alvo.Context.Health.Max,
                "o pouso da estocada deveria ter causado dano em área no manequim próximo");
            Verificar(
                (combate.ActiveLocks & (ActionLock.Movement | ActionLock.Rotation))
                == (ActionLock.Movement | ActionLock.Rotation),
                "o pouso da estocada deveria travar Movimento e Rotação para a recuperação");

            Concluir();
            return;
        }

        if (_quadroDaFase > QuadrosDeTimeoutDoMergulho)
        {
            Godot.Input.ActionRelease(InputActionNames.AttackBasic);
            Verificar(false, "a estocada de queda deveria ter aterrissado dentro do tempo limite");
            Concluir();
        }
    }

    private void TeleportarNoAr(CharacterController quem, Vector3 posicao)
    {
        quem.Velocity = Vector3.Zero;
        quem.GlobalPosition = posicao;
    }

    private void TeleportarNoChao()
    {
        _jogador!.Velocity = Vector3.Zero;
        _jogador.GlobalPosition = AreaAberta + new Vector3(0f, 0.1f, 0f);
        _alvo!.Velocity = Vector3.Zero;
        _alvo.GlobalPosition = AreaAberta + new Vector3(0f, 0.1f, 1.5f);
    }

    private void Reposicionar()
    {
        var frente = -_jogador!.GlobalTransform.Basis.Z;
        var plano = new Vector3(frente.X, 0f, frente.Z).Normalized();
        _alvo!.GlobalPosition = _jogador.GlobalPosition + (plano * 1.5f);
    }

    /// <summary>Mesma ideia de <see cref="Reposicionar"/>, mas preservando a altura do jogador -- os dois seguem no ar.</summary>
    private void ReposicionarNoAr()
    {
        // Só X/Z, nunca a altura: forçar a altura do manequim para bater
        // com a do jogador a cada quadro destruiria a própria queda dele
        // (o `Velocity.Y` que este probe mede) -- um teleporte de posição
        // não mexe em velocidade diretamente, mas colidir com o resultado
        // dele a cada quadro é exatamente o tipo de ruído que a medição do
        // impulso não pode ter por perto. Deixar a altura por conta da
        // física de verdade do manequim é o que faz a medição confiável.
        var frente = -_jogador!.GlobalTransform.Basis.Z;
        var plano = new Vector3(frente.X, 0f, frente.Z).Normalized();
        var alvoXZ = new Vector3(_jogador.GlobalPosition.X, 0f, _jogador.GlobalPosition.Z) + (plano * 1.2f);
        _alvo!.GlobalPosition = new Vector3(alvoXZ.X, _alvo.GlobalPosition.Y, alvoXZ.Z);
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[aereo] FALHA: {mensagem}");
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
            GD.PrintErr($"[aereo] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[aereo] todas as verificações passaram");
        GetTree().Quit();
    }

    private void Procurar(Node no)
    {
        foreach (var filho in no.GetChildren())
        {
            if (filho is CharacterController c)
            {
                if (c.Team == Team.Player && c.Context?.Combat is not null)
                    _jogador ??= c;
                else if (c.Team == Team.Enemy && c.Context?.Health is not null)
                    _alvo ??= c;
            }

            Procurar(filho);
        }
    }
}
