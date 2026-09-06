using System.Collections.Generic;
using Contenda.Camera;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Components.Movement;
using Contenda.Core;
using Contenda.Weapons;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica pulo e dash na árvore real, sem depender só de matemática isolada.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem <c>JumpState</c>, <c>DashState</c> e
/// <c>MovementMath</c> (altura, coyote time, jump buffer, gravidade
/// assimétrica) isolados, com controle exato do relógio. O que eles não
/// alcançam é a fiação: um <c>MoveAndSlide</c> de verdade contra o chão real
/// da <c>Arena.tscn</c>, o <c>ActionLock</c> de rotação do dash aplicado pelo
/// <c>CombatComponent</c>, e a invulnerabilidade do dash bloqueando um golpe
/// de verdade no <c>HealthComponent</c>.
///
/// Usa uma <see cref="MovementSettings"/> própria, trocada em runtime via
/// <c>SwitchDefinition</c> (mesmo mecanismo da tecla de debug do ticket 12):
/// o `DashCooldown` real (1,2 s) tornaria o teste de "um dash por pulo no ar"
/// ambíguo entre "bloqueado pela recarga" e "bloqueado por já ter usado" — uma
/// recarga bem mais curta isola a regra que este probe realmente quer testar.
///
/// A "Plataforma" de <c>Arena.tscn</c> (12×12 m, topo a 1,5 m) faz o papel da
/// borda para o teste de coyote time: o personagem é teleportado para o ar
/// logo depois da borda, sem passar pela varredura de <c>WASD</c> relativa à
/// câmera.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/MovementProbe.tscn
/// </code>
/// </remarks>
public sealed partial class MovementProbe : Node
{
    /// <summary>Cena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    private const float Gravity = 22f;
    private const float JumpHeight = 2.2f;
    private const float FallGravityScale = 1.6f;
    private const float CoyoteTime = 0.12f;
    private const float JumpBufferTime = 0.12f;
    private const float AirControlFactor = 0.5f;
    private const float DashDistance = 5f;
    private const float DashDuration = 0.18f;
    private const float DashCooldown = 0.05f;
    private const float DashInvulnerability = 0.12f;

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private MovementComponent? _movimento;
    private HealthComponent? _vida;
    private CameraRig? _camera;

    private int _quadro;
    private int _fase = -1;
    private int _quadroDaFase;

    private float _picoY;
    private int _quadroDoPico;
    private bool _passouDoPico;
    private Vector3 _posicaoAntes;
    private float _rotacaoAntes;
    private float _manaAntes;
    private float _vidaAntesDoGolpe;
    private float _deslocamentoNoSolo;
    private float _cameraYAntes;
    private float _cameraYMaxDesvio;
    private int _fase6Etapa;
    private int _quadroDoSegundoPedido;
    private bool _jumpBufferPressionado;
    private int _quadroDoPedidoDeBuffer;
    private bool _dashDetectado;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[movimento] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        if (_jogador?.Context is not { Movement: { } movimento, Health: { } vida, Mana: { } mana } || _camera is null)
        {
            GD.PrintErr("[movimento] FALHA: não encontrei jogador com Movement/Health/Mana, ou a câmera.");
            GetTree().Quit(1);
            return;
        }

        _movimento = movimento;
        _vida = vida;

        // Settings de teste, não os do Swordsman: ver o comentário da classe
        // sobre por que o DashCooldown real ambiguaria o teste de "uma vez
        // por pulo no ar". Weapon precisa ser um `.tres` de verdade -- o
        // CombatComponent de Character.tscn não tem Fallback, e um
        // WeaponDefinition() vazio (ComboSteps=[]) derruba o MeleeWeapon na
        // hora de montar (ver o dano à parte já sinalizado para correção).
        _jogador.SwitchDefinition(new CharacterDefinition
        {
            Weapon = GD.Load<WeaponDefinition>("res://data/weapons/sword.tres"),
            Movement = new MovementSettings
            {
                Gravity = Gravity,
                JumpHeight = JumpHeight,
                FallGravityScale = FallGravityScale,
                CoyoteTime = CoyoteTime,
                JumpBufferTime = JumpBufferTime,
                AirControlFactor = AirControlFactor,
                DashDistance = DashDistance,
                DashDuration = DashDuration,
                DashCooldown = DashCooldown,
                DashInvulnerability = DashInvulnerability,
            },
        });

        GD.Print($"[movimento] jogador pronto; mana inicial {mana.Current:0}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_movimento is null || _vida is null || _jogador is null)
            return;

        _quadro++;

        if (_fase < 0)
        {
            // Assentamento: deixa o SwitchDefinition e a física acomodarem
            // antes do primeiro teste.
            if (_quadro >= 10)
                AvancarFase();

            return;
        }

        _quadroDaFase++;

        switch (_fase)
        {
            case 0: FaseAlturaDoPulo(); break;
            case 1: FaseCoyoteTime(); break;
            case 2: FaseJumpBuffer(); break;
            case 3: FaseControleNoAr(); break;
            case 4: FaseDash(); break;
            case 5: FaseInvulnerabilidadeDoDash(); break;
            case 6: FaseDashUmaVezNoAr(); break;
            default: Concluir(); break;
        }
    }

    // --- fase 0: altura do pulo, queda mais pesada que a subida, câmera estável ---

    private void FaseAlturaDoPulo()
    {
        if (_quadroDaFase == 1)
        {
            TeleportarParado(new Vector3(0f, 0.1f, 10f));
            _picoY = 0f;
            _quadroDoPico = 0;
            _passouDoPico = false;
            _cameraYAntes = _camera!.GlobalPosition.Y;
            _cameraYMaxDesvio = 0f;
            return;
        }

        if (_quadroDaFase == 3)
        {
            PressionarEsoltar(InputActionNames.Jump);
            return;
        }

        if (_quadroDaFase > 3 && _quadroDaFase <= 90 && !_jogador!.IsOnFloor())
        {
            var alturaAtual = _jogador.GlobalPosition.Y - 0.1f;

            if (alturaAtual > _picoY)
            {
                _picoY = alturaAtual;
                _quadroDoPico = _quadroDaFase;
            }
            else if (!_passouDoPico && alturaAtual < _picoY - 0.01f)
            {
                _passouDoPico = true;
            }

            _cameraYMaxDesvio = Mathf.Max(_cameraYMaxDesvio, Mathf.Abs(_camera!.GlobalPosition.Y - _cameraYAntes));
        }

        // Aterrissou de volta (ou o teto de quadros bateu): compara quanto
        // tempo levou para SUBIR até o pico com quanto levou para DESCER de
        // volta -- com a queda mais pesada (FallGravityScale > 1), descer a
        // MESMA altura leva menos quadros que subir, spec 16 §3.
        if (_passouDoPico && (_jogador!.IsOnFloor() || _quadroDaFase == 90))
        {
            var quadrosSubindo = _quadroDoPico - 3;
            var quadrosDescendo = _quadroDaFase - _quadroDoPico;

            Verificar(Mathf.Abs(_picoY - JumpHeight) < JumpHeight * 0.2f,
                $"o pulo deveria alcançar ~{JumpHeight} m de altura; alcançou {_picoY:0.00} m");

            Verificar(quadrosDescendo < quadrosSubindo,
                $"a queda deveria ser mais rápida que a subida (mesma altura); subiu em {quadrosSubindo} quadros, desceu em {quadrosDescendo}");

            // A câmera segue Y bem mais devagar E ignora variação menor que o
            // dead zone (2,4 m > altura do pulo, combat_camera.tres) -- um
            // pulo sozinho não deveria mover a câmera quase nada.
            Verificar(_cameraYMaxDesvio < 0.5f,
                $"a câmera não deveria balançar num pulo só; desviou {_cameraYMaxDesvio:0.00} m");

            AvancarFase();
        }
        else if (_quadroDaFase >= 90)
        {
            Verificar(false, "o pulo deveria ter subido, descido e aterrissado bem antes de 90 quadros");
            AvancarFase();
        }
    }

    // --- fase 1: coyote time ---

    private void FaseCoyoteTime()
    {
        if (_quadroDaFase == 1)
        {
            // Logo além da borda leste da Plataforma (topo em X=-10, Y=1,5) --
            // ar aberto, chão de verdade uns 1,5 m abaixo.
            TeleportarParado(new Vector3(-9.5f, 1.5f, -16f));
            return;
        }

        // Poucos quadros depois de deixar de haver chão embaixo -- dentro da
        // janela de coyote (0,12 s = ~7 quadros) -- ainda dá para pular.
        if (_quadroDaFase == 4)
        {
            Verificar(!_jogador!.IsOnFloor(), "já deveria estar no ar, sem chão embaixo");
            PressionarEsoltar(InputActionNames.Jump);
            return;
        }

        if (_quadroDaFase == 6)
        {
            Verificar(_movimento!.Velocity.Y > 1f,
                $"coyote time deveria ter deixado pular pouco depois de sair da borda; velocidade Y ficou {_movimento.Velocity.Y:0.00}");
            AvancarFase();
        }
    }

    // --- fase 2: jump buffer ---

    private void FaseJumpBuffer()
    {
        if (_quadroDaFase == 1)
        {
            _jumpBufferPressionado = false;

            // Cai de uma altura boa -- dá tempo de sobra para deixar o pedido
            // só para perto do chão, dentro da janela do buffer (0,12 s), e
            // não logo de cara (o que testaria coyote de saída, não buffer de
            // chegada).
            TeleportarParado(new Vector3(0f, 6f, 10f));
            return;
        }

        // Pede o pulo só quando estiver perto do chão -- perto o bastante
        // para aterrissar bem dentro da janela do buffer (0,12 s a 60 Hz são
        // ~7 quadros; a essa altura e velocidade de queda faltam bem menos
        // que isso), mas ainda no ar de verdade, não já apoiado.
        if (!_jumpBufferPressionado)
        {
            if (_jogador!.GlobalPosition.Y < 0.6f)
            {
                PressionarEsoltar(InputActionNames.Jump);
                _jumpBufferPressionado = true;
                _quadroDoPedidoDeBuffer = _quadroDaFase;
            }

            return;
        }

        // NÃO espera "IsOnFloor() == true" para conferir: o próprio pulo
        // bufferizado, ao disparar, tira o personagem do chão de novo dentro
        // do MESMO quadro de física em que aterrissou -- e como este probe só
        // enxerga o resultado de um Tick um quadro depois de ele acontecer
        // (ver os comentários mais acima sobre a defasagem de entrada), o
        // instante exato em que `IsOnFloor()` fica verdadeiro nunca chega a
        // ser observado, só o antes (caindo) e o depois (subindo de novo).
        // Observar a velocidade vertical direto, em vez do chão, não depende
        // de pegar esse instante entre dois quadros.
        if (_movimento!.Velocity.Y > 1f)
        {
            AvancarFase();
            return;
        }

        // Não disparou ainda: só depois de alguns quadros de folga (a mesma
        // defasagem de um quadro inteiro que a entrada sintética já exige em
        // todo o resto deste probe) é que "velocidade pequena e perto do
        // chão" deixa de ser "ainda vai disparar" e passa a ser "realmente
        // não disparou".
        if (_quadroDaFase - _quadroDoPedidoDeBuffer > 3
            && Mathf.Abs(_movimento.Velocity.Y) < 0.5f
            && _jogador!.GlobalPosition.Y < 0.1f)
        {
            Verificar(false, "o pulo bufferizado deveria ter disparado ao aterrissar, e a queda já terminou sem subir de novo");
            AvancarFase();
            return;
        }

        if (_quadroDaFase > 300)
            Verificar(false, "deveria ter aterrissado bem antes disto");
    }

    // --- fase 3: controle reduzido, mas existente, no ar ---

    private void FaseControleNoAr()
    {
        if (_quadroDaFase == 1)
        {
            TeleportarParado(new Vector3(0f, 0.1f, 10f));
            return;
        }

        if (_quadroDaFase == 3)
        {
            _posicaoAntes = _jogador!.GlobalPosition;
            Godot.Input.ActionPress(InputActionNames.MoveUp);
            return;
        }

        if (_quadroDaFase == 13)
        {
            Godot.Input.ActionRelease(InputActionNames.MoveUp);
            _deslocamentoNoSolo = new Vector3(_jogador!.GlobalPosition.X, 0f, _jogador.GlobalPosition.Z)
                .DistanceTo(new Vector3(_posicaoAntes.X, 0f, _posicaoAntes.Z));
            return;
        }

        if (_quadroDaFase == 15)
        {
            TeleportarParado(new Vector3(0f, 0.1f, 10f));
            PressionarEsoltar(InputActionNames.Jump);
            return;
        }

        if (_quadroDaFase == 18)
        {
            // Já subindo havia alguns quadros -- bem no ar, sem tocar o chão.
            Verificar(!_jogador!.IsOnFloor(), "deveria estar no ar para medir o controle aéreo");
            _posicaoAntes = _jogador.GlobalPosition;
            Godot.Input.ActionPress(InputActionNames.MoveUp);
            return;
        }

        if (_quadroDaFase == 28)
        {
            Godot.Input.ActionRelease(InputActionNames.MoveUp);
            var deslocamentoNoAr = new Vector3(_jogador!.GlobalPosition.X, 0f, _jogador.GlobalPosition.Z)
                .DistanceTo(new Vector3(_posicaoAntes.X, 0f, _posicaoAntes.Z));

            Verificar(deslocamentoNoAr > 0.05f, $"controle no ar não deveria ser ZERO; deslocou {deslocamentoNoAr:0.000} m");
            Verificar(deslocamentoNoAr < _deslocamentoNoSolo,
                $"controle no ar deveria ser MENOR que no solo; ar={deslocamentoNoAr:0.00} m, solo={_deslocamentoNoSolo:0.00} m");

            AvancarFase();
        }
    }

    // --- fase 4: dash -- distância, direção travada, sem custo de mana ---

    private void FaseDash()
    {
        if (_quadroDaFase == 1)
        {
            TeleportarParado(new Vector3(0f, 0.1f, 10f));
            _manaAntes = _jogador!.Context!.Mana!.Current;
            _posicaoAntes = _jogador.GlobalPosition;
            _dashDetectado = false;

            // SEM segurar direção nenhuma ainda: entradas sintéticas deste
            // probe não têm a mesma latência de borda que um toque de tecla
            // de verdade tem por trás de IsActionJustPressed, e disparar as
            // duas juntas deixava a rotação escapar por um quadro ANTES do
            // dash sequer ser detectado -- um artefato de como este probe
            // simula entrada, não do MovementComponent. Testar "não dá para
            // curvar" com a direção pressionada só DEPOIS do dash já
            // confirmado em andamento isola exatamente essa regra.
            PressionarEsoltar(InputActionNames.Dash);
            return;
        }

        // Aguarda o dash começar de verdade antes de tentar curvar.
        if (!_dashDetectado)
        {
            if (_movimento!.IsDashing)
            {
                _dashDetectado = true;
                _rotacaoAntes = _jogador!.Rotation.Y;
                Godot.Input.ActionPress(InputActionNames.MoveLeft);
            }
            else if (_quadroDaFase > 10)
            {
                Verificar(false, "o dash deveria ter começado bem antes disto");
            }

            return;
        }

        // Já detectado: espera o dash TERMINAR sozinho, e mede posição e
        // rotação EXATAMENTE nesse instante -- não alguns quadros depois. Com
        // MoveLeft ainda seguro, a rotação retoma assim que a trava solta, e a
        // velocidade de saída do dash (~28 m/s) leva vários quadros para
        // desacelerar de volta à velocidade normal de WASD -- medir tarde
        // demais confundiria essa inércia residual com o próprio dash.
        if (_movimento!.IsDashing)
        {
            if (_quadroDaFase > 30)
                Verificar(false, "o dash deveria ter terminado bem antes disto");

            return;
        }

        Godot.Input.ActionRelease(InputActionNames.MoveLeft);

        Verificar(Mathf.IsEqualApprox(_jogador!.Rotation.Y, _rotacaoAntes, 0.01f),
            $"não deveria dar para curvar durante o dash; rotação foi de {_rotacaoAntes:0.000} para {_jogador.Rotation.Y:0.000}");

        var deslocamento = new Vector3(_jogador.GlobalPosition.X, 0f, _jogador.GlobalPosition.Z)
            .DistanceTo(new Vector3(_posicaoAntes.X, 0f, _posicaoAntes.Z));
        Verificar(Mathf.Abs(deslocamento - DashDistance) < DashDistance * 0.25f,
            $"o dash deveria avançar ~{DashDistance} m; avançou {deslocamento:0.00} m");

        Verificar(Mathf.IsEqualApprox(_jogador.Context!.Mana!.Current, _manaAntes),
            $"o dash não deveria consumir mana; mana foi de {_manaAntes:0} para {_jogador.Context.Mana.Current:0}");

        AvancarFase();
    }

    // --- fase 5: invulnerabilidade do dash bloqueia um golpe de verdade ---

    private void FaseInvulnerabilidadeDoDash()
    {
        if (_quadroDaFase == 1)
        {
            TeleportarParado(new Vector3(0f, 0.1f, 10f));
            _dashDetectado = false;

            // A recarga da fase anterior (0,05 s de teste) já passou de sobra.
            PressionarEsoltar(InputActionNames.Dash);
            return;
        }

        if (!_dashDetectado)
        {
            if (!_movimento!.IsDashing)
            {
                if (_quadroDaFase > 10)
                    Verificar(false, "o dash deveria ter começado bem antes disto");

                return;
            }

            _dashDetectado = true;
            Verificar(_vida!.IsInvulnerable, "o dash deveria conceder invulnerabilidade na hora");

            _vidaAntesDoGolpe = _vida.Current;
            _vida.ApplyDamage(new DamageInfo(
                Amount: 30f, Type: DamageType.Physical, HitPoint: Vector3.Zero,
                Direction: Vector3.Forward, Knockback: 0f, SourceId: 0UL,
                SourceTag: "movement_probe", IsCritical: false));
            return;
        }

        // Um quadro depois do golpe: ResolveQueue já rodou pelo menos uma vez.
        Verificar(Mathf.IsEqualApprox(_vida!.Current, _vidaAntesDoGolpe),
            $"a invulnerabilidade do dash deveria ter bloqueado o golpe; vida foi de {_vidaAntesDoGolpe:0} para {_vida.Current:0}");

        // Só avança depois que ESTE dash de verdade terminar -- a fase
        // seguinte (ticket 17, "um dash por pulo no ar") também dispara um
        // dash logo de cara, e `podeComecar` exige `!_dash.IsActive`. Avançar
        // cedo demais faria a fase 6 só "herdar" este dash ainda em
        // andamento, sem nunca disparar um de verdade -- e a contagem de "já
        // usado no ar" nunca seria marcada.
        if (!_movimento!.IsDashing)
            AvancarFase();
    }

    // --- fase 6: dash só uma vez por pulo, no ar ---

    private void FaseDashUmaVezNoAr()
    {
        if (_quadroDaFase == 1)
        {
            TeleportarParado(new Vector3(0f, 0.1f, 10f));
            PressionarEsoltar(InputActionNames.Jump);
            _fase6Etapa = 0;
            return;
        }

        switch (_fase6Etapa)
        {
            case 0: // esperando decolar, para então disparar o primeiro dash
                if (!_jogador!.IsOnFloor())
                {
                    PressionarEsoltar(InputActionNames.Dash);
                    _fase6Etapa = 1;
                }
                else if (_quadroDaFase > 20)
                {
                    Verificar(false, "deveria ter saído do chão bem antes disto");
                }

                return;

            case 1: // esperando o primeiro dash começar de verdade
                if (_movimento!.IsDashing)
                {
                    _fase6Etapa = 2;
                }
                else if (_quadroDaFase > 30)
                {
                    Verificar(false, "o primeiro dash no ar deveria ter começado bem antes disto");
                }

                return;

            case 2: // esperando o primeiro dash terminar, para tentar um segundo
                if (!_movimento!.IsDashing)
                {
                    // Recarga curta de teste (0,05 s) já zerada a esta altura --
                    // só a regra de "um por pulo" pode bloquear a partir daqui.
                    Verificar(_movimento.DashCooldownRemaining <= 0f, "a recarga de teste já deveria ter zerado");
                    Verificar(!_jogador!.IsOnFloor(), "ainda deveria estar no ar para testar a segunda tentativa");

                    PressionarEsoltar(InputActionNames.Dash);
                    _quadroDoSegundoPedido = _quadroDaFase;
                    _fase6Etapa = 3;
                }
                else if (_quadroDaFase > 60)
                {
                    Verificar(false, "o primeiro dash deveria ter terminado bem antes disto");
                }

                return;

            case 3: // confirma que o segundo pedido, no MESMO pulo, não pegou
                if (_quadroDaFase - _quadroDoSegundoPedido >= 5)
                {
                    Verificar(!_movimento!.IsDashing,
                        "um segundo dash no MESMO pulo não deveria começar, mesmo com a recarga livre");
                    _fase6Etapa = 4;
                }

                return;

            case 4: // espera aterrissar, então tenta um dash novo -- deveria valer de novo
                if (!_jogador!.IsOnFloor())
                {
                    if (_quadroDaFase > 400)
                        Verificar(false, "deveria ter aterrissado bem antes disto");

                    return;
                }

                PressionarEsoltar(InputActionNames.Dash);
                _quadroDoSegundoPedido = _quadroDaFase;
                _fase6Etapa = 5;
                return;

            case 5: // aterrissar deveria ter liberado o dash de novo
                if (_movimento!.IsDashing)
                {
                    Concluir();
                    return;
                }

                if (_quadroDaFase - _quadroDoSegundoPedido > 10)
                    Verificar(false, "aterrissar deveria ter liberado o dash de novo, e não liberou");

                return;
        }
    }

    private void TeleportarParado(Vector3 posicao)
    {
        _jogador!.Velocity = Vector3.Zero;
        _jogador.GlobalPosition = posicao;
    }

    /// <remarks>
    /// Pressiona e solta na hora. O `PlayerInputController` só enxerga a
    /// borda de subida um quadro de física depois de qualquer jeito (entrada
    /// sintética não passa pelo mesmo caminho que um toque de tecla de
    /// verdade) -- por isso todo teste deste probe que depende de uma borda
    /// espera pelo EFEITO (`IsDashing`, uma velocidade vertical nova), nunca
    /// por um número de quadro fixo contado a partir do próprio `Pressionar`.
    /// </remarks>
    private void PressionarEsoltar(StringName acao)
    {
        Godot.Input.ActionPress(acao);
        Godot.Input.ActionRelease(acao);
    }

    private void AvancarFase()
    {
        _fase++;
        _quadroDaFase = 0;
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[movimento] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[movimento] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[movimento] todas as verificações passaram");
        GetTree().Quit();
    }

    private void Procurar(Node no)
    {
        if (no is CameraRig rig)
            _camera = rig;

        if (no is CharacterController c && c.Team == Team.Player && c.Context is not null)
            _jogador ??= c;

        foreach (var filho in no.GetChildren())
            Procurar(filho);
    }
}
