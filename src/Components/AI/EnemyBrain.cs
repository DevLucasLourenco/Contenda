using System.Collections.Generic;
using Contenda.Camera;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.Input;
using Godot;

namespace Contenda.Components.AI;

/// <summary>
/// Produz <see cref="IntentFrame"/> a partir de percepção e estado, em vez de
/// teclado -- é isto que faz um inimigo reusar o mesmo <c>MovementComponent</c>/
/// <c>CombatComponent</c> do jogador sem nenhuma locomoção duplicada. Ver
/// docs/specs/09-inimigos-e-ia.md §1-2 e o ticket 22.
/// </summary>
/// <remarks>
/// Substitui o <c>PlayerInputController</c> como fonte de intenção deste
/// personagem -- <c>CharacterController</c> chama <see cref="Poll"/> no lugar
/// de <c>PlayerInputController.Poll()</c> quando este componente existe.
///
/// A decisão pura de "qual o próximo estado" mora em
/// <see cref="EnemyStateMachine"/> (POCO, testado em xUnit). Este componente
/// só produz os SINAIS que ela consome (percepção, alcance, se o golpe
/// terminou) e traduz o estado resultante de volta em <see cref="IntentFrame"/>
/// -- a fronteira com a engine que não dá para tirar dali.
/// </remarks>
public sealed partial class EnemyBrain : Node, ICharacterComponent
{
    /// <summary>Percepção, alcance e tempos deste inimigo.</summary>
    [Export] public EnemyDefinition Definition { get; set; } = new();

    /// <summary>Altura do raycast de linha de visão, para não mirar nos pés.</summary>
    [Export(PropertyHint.Range, "0,3,0.1")] public float EyeHeight { get; set; } = 1f;

    private CharacterContext? _contexto;
    private EnemyStateMachine _maquina = null!;
    private Perception _percepcao = null!;

    /// <summary>
    /// Além de quantos metros o inimigo passa a "pensar" a
    /// <see cref="IntervaloDeLodSegundos"/> em vez de todo quadro. Ver spec
    /// 09 §9, ticket 23.
    /// </summary>
    [Export(PropertyHint.Range, "5,60,1")] public float DistanciaDeLod { get; set; } = 35f;

    /// <summary>Intervalo de "pensamento" para um inimigo distante -- 0,2 s = 5 Hz. Ver spec 09 §9.</summary>
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float IntervaloDeLodSegundos { get; set; } = 0.2f;

    /// <summary>Raio de separação entre inimigos próximos, em metros. Ver spec 09 §4.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.1")] public float RaioDeSeparacao { get; set; } = 1.2f;

    /// <summary>Peso da força de separação. Ver spec 09 §4.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float PesoDeSeparacao { get; set; } = 0.35f;

    private float _windupRestante;
    private bool _golpeSolicitado;
    private float _temporizadorDeMorte;
    private bool _liberado;

    /// <remarks>
    /// Nasce já "vencido" (maior que <see cref="IntervaloDeLodSegundos"/>),
    /// não em zero -- o primeiro <see cref="Poll"/> da vida de um inimigo
    /// sempre pensa de verdade, mesmo que já nasça longe do jogador.
    /// </remarks>
    private float _relogioDePensamento = float.PositiveInfinity;
    private IntentFrame _ultimaIntencao = IntentFrame.Idle;

    /// <summary>
    /// Reaproveitado a cada <see cref="CalcularForcaDeSeparacao"/>, nunca uma
    /// `List` nova por quadro -- ver o remark daquele método.
    /// </summary>
    private readonly List<Vector3> _vizinhosBuffer = [];

    /// <summary>Quantas vezes este inimigo pensou de verdade (não reaproveitou o quadro anterior). Para o probe.</summary>
    public int PensamentosCompletos { get; private set; }

    /// <summary>Em qual fase da percepção/combate este inimigo está. Para o probe/depuração.</summary>
    public EnemyState Estado => _maquina.Estado;

    /// <summary>
    /// Quem devolve este inimigo ao estoque ao fim da morte. Nulo até o
    /// <c>EnemyPool</c> que o instanciou marcar a própria referência aqui --
    /// não vem do <c>CharacterContext</c> porque não é uma relação ENTRE
    /// componentes do mesmo personagem, é o dono externo que o criou. Ver
    /// ticket 25, spec 09 §8.
    /// </summary>
    internal EnemyPool? Pool { get; set; }

    public void Bind(CharacterContext contexto)
    {
        if (_contexto?.Health is not null)
        {
            _contexto.Health.Damaged -= AoApanhar;
            _contexto.Health.Died -= AoMorrer;
        }

        _contexto = contexto;

        if (_contexto.Health is not null)
        {
            _contexto.Health.Damaged += AoApanhar;
            _contexto.Health.Died += AoMorrer;
        }

        _percepcao ??= new Perception(this);
    }

    /// <remarks>
    /// <c>NavigationMotor</c>/<c>AttackTelegraph</c> só são lidos AQUI, na
    /// segunda passada do contêiner, nunca em <c>Bind</c>: o
    /// <c>CharacterContext</c> só garante os dois preenchidos depois que
    /// TODOS os `Bind` já rodaram -- ler cedo demais dependeria da ordem
    /// entre nós irmãos, que a primeira passada explicitamente não garante.
    /// </remarks>
    public void Configure(CharacterDefinition definicao)
    {
        var def = Definition;
        _maquina = new EnemyStateMachine(def.AlertDuration, def.AttackCooldown, def.StaggerDuration, def.LoseTargetDelay);
        _windupRestante = 0f;
        _golpeSolicitado = false;
        _relogioDePensamento = float.PositiveInfinity;
        _ultimaIntencao = IntentFrame.Idle;
        PensamentosCompletos = 0;
        _contexto?.AttackTelegraph?.DesligarAviso();
        AnunciarComoChefeSeForCaso();
    }

    public override void _ExitTree()
    {
        if (_contexto?.Health is not null)
        {
            _contexto.Health.Damaged -= AoApanhar;
            _contexto.Health.Died -= AoMorrer;
        }
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        _maquina = new EnemyStateMachine(
            Definition.AlertDuration, Definition.AttackCooldown, Definition.StaggerDuration, Definition.LoseTargetDelay);
        _windupRestante = 0f;
        _golpeSolicitado = false;
        _temporizadorDeMorte = 0f;
        _liberado = false;
        _relogioDePensamento = float.PositiveInfinity;
        _ultimaIntencao = IntentFrame.Idle;
        PensamentosCompletos = 0;
        _contexto?.AttackTelegraph?.DesligarAviso();
        _contexto?.Combat?.Cancel();
        AnunciarComoChefeSeForCaso();
    }

    /// <summary>
    /// Se esta <see cref="EnemyDefinition"/> marca <see cref="EnemyDefinition.IsBoss"/>,
    /// anuncia-se em <see cref="GameSession.BossBody"/> -- mesmo espírito de
    /// <c>PlayerBody</c> (o personagem se anuncia, ninguém procura por
    /// caminho), mas repetido tanto em <see cref="Configure"/> (nascimento)
    /// quanto em <see cref="ResetForSpawn"/> (reciclagem): ao contrário de
    /// <c>Bind</c>/<c>Configure</c>, que só rodam uma vez na vida inteira de
    /// um nó pooled, <c>ResetForSpawn</c> roda de novo a CADA reaproveitamento
    /// -- sem repetir aqui, um chefe reciclado para servir de chefe de novo
    /// (onda futura) nunca voltaria a se anunciar. Ticket 26.
    /// </summary>
    private void AnunciarComoChefeSeForCaso()
    {
        if (Definition.IsBoss)
            ServiceLocator.Session.BossBody = _contexto?.Owner;
    }

    /// <summary>
    /// Lê percepção e estado, e monta a intenção deste quadro.
    /// </summary>
    /// <remarks>
    /// Chamado pelo <c>CharacterController</c> no lugar de
    /// <c>PlayerInputController.Poll()</c> -- mesmo papel, fonte diferente.
    /// </remarks>
    public IntentFrame Poll(float delta)
    {
        // Morto não percebe, não persegue, não ataca -- só espera o próprio
        // corpo terminar de "animar" (o placeholder do ticket 25, sem arte
        // ainda) antes de voltar ao pool. Fora daqui de propósito: mesmo sem
        // alvo nenhum, o resto de Poll() ainda tentaria ler `_contexto.Body`
        // normalmente, e não há percepção nenhuma para rodar num cadáver.
        if (_maquina.Estado == EnemyState.Death)
            return AtualizarMorte(delta);

        var alvo = ServiceLocator.Session.PlayerBody;
        if (alvo is null || !GodotObject.IsInstanceValid(alvo) || alvo.Context?.Health is not { IsAlive: true })
            return IntentFrame.Idle;

        var corpo = _contexto!.Body;
        var distancia = corpo.GlobalPosition.DistanceTo(alvo.GlobalPosition);

        // LOD de IA (spec 09 §9, ticket 23): distante, pensa a
        // IntervaloDeLodSegundos (5 Hz) em vez de todo quadro (60 Hz) --
        // reaproveita a ÚLTIMA intenção calculada em vez de recalcular
        // percepção (um raycast) e rota a cada quadro para um inimigo que o
        // jogador mal consegue ver na tela. `_relogioDePensamento` nasce
        // "vencido" (Configure/ResetForSpawn), então o primeiro Poll da vida
        // sempre pensa de verdade, mesmo já nascendo longe.
        _relogioDePensamento += delta;
        if (distancia > DistanciaDeLod && _relogioDePensamento < IntervaloDeLodSegundos)
            return _ultimaIntencao;

        // Tempo desde o ÚLTIMO pensamento de verdade, não só este quadro: um
        // inimigo que só pensou de novo depois de 5 quadros pulados (LOD)
        // precisa alimentar `Advance`/o preparo do golpe com os 5 quadros
        // INTEIROS -- se recebessem só o delta deste quadro, o cronômetro
        // interno de LoseTargetDelay (por exemplo) andaria 5x mais devagar
        // que o relógio de verdade, e um inimigo distante nunca desistiria
        // de perseguir dentro do prazo da spec.
        var deltaEfetivo = _relogioDePensamento;
        _relogioDePensamento = 0f;
        PensamentosCompletos++;

        var origemVisao = corpo.GlobalPosition + (Vector3.Up * EyeHeight);
        var alvoVisao = alvo.GlobalPosition + (Vector3.Up * EyeHeight);

        // Enquanto ainda não percebeu ninguém, a peneira é o raio de detecção
        // (menor); depois de já estar de olho, vale o raio de perda (maior) --
        // é o que evita ioiô de estado bem na borda do raio inicial.
        var raio = _maquina.Estado == EnemyState.Idle ? Definition.DetectionRadius : Definition.LoseTargetRadius;
        var alvoVisivel = _percepcao.Visivel(origemVisao, alvoVisao, raio, PhysicsLayers.World);
        var dentroDoAlcance = distancia <= Definition.AttackRange;

        // Progride o preparo do golpe ANTES de avançar a máquina: este quadro
        // pode ser exatamente o que termina o windup e pede o golpe de
        // verdade, e a máquina precisa saber se o golpe JÁ terminou (não se
        // vai terminar) para decidir sair de Attack.
        var (ataqueTerminou, pedirAtaqueAgora) = AtualizarPreparoDoGolpe(deltaEfetivo);

        // `IsGroundedConfiavel`, não `IsGrounded` cru -- este último fica um
        // quadro atrasado bem no quadro exato de um lançamento vertical
        // (golpe que lança um grunt PARADO no chão), o que derrubaria de
        // volta para `Chase` antes mesmo do corpo sair do chão de verdade.
        // Ver o próprio `MovementComponent.IsGroundedConfiavel` e ticket 24,
        // spec 16 §6.
        var estaNoChao = _contexto.Movement?.IsGroundedConfiavel ?? true;

        var estadoAntes = _maquina.Estado;
        _maquina.Advance(deltaEfetivo, alvoVisivel, dentroDoAlcance, ataqueTerminou, estaNoChao);

        if (_maquina.Estado != estadoAntes)
            AoTrocarDeEstado(estadoAntes, _maquina.Estado);

        _ultimaIntencao = MontarIntencao(corpo, alvo, pedirAtaqueAgora, deltaEfetivo);
        return _ultimaIntencao;
    }

    /// <summary>
    /// Conta o tempo de corpo caído e pede ao pool para me devolver ao
    /// estoque assim que passar de <see cref="EnemyDefinition.DeathDuration"/>.
    /// </summary>
    /// <remarks>
    /// `_liberado` evita pedir de novo a cada quadro depois do primeiro
    /// pedido -- <c>Pool.Release</c> desliga <c>ProcessMode</c> deste nó, o
    /// que já impediria um novo <c>Poll</c> no quadro seguinte, mas nada
    /// garante que a desativação seja síncrona em toda situação, e pedir
    /// duas vezes seria devolver o mesmo inimigo duas vezes à pilha livre.
    ///
    /// `Release` é chamado ADIADO (<c>CallDeferred</c>), nunca direto daqui:
    /// isto roda dentro do PRÓPRIO <c>Poll</c>, que é só o PRIMEIRO passo do
    /// <c>_PhysicsProcess</c> do contêiner (spec 01 §6) -- `Movement.Tick`
    /// (passo 3) ainda roda DEPOIS, no mesmo quadro. `Release` zera
    /// colisão/`ProcessMode` na hora; chamado síncrono aqui, o corpo perderia
    /// o espaço físico ANTES do próprio `MoveAndSlide` deste quadro rodar --
    /// "body->get_space() is null". Adiar para depois do quadro inteiro
    /// terminar evita a corrida.
    /// </remarks>
    private IntentFrame AtualizarMorte(float delta)
    {
        if (_liberado)
            return IntentFrame.Idle;

        _temporizadorDeMorte += delta;
        if (_temporizadorDeMorte >= Definition.DeathDuration)
        {
            _liberado = true;
            Pool?.CallDeferred(nameof(EnemyPool.Release), _contexto!.Owner);
        }

        return IntentFrame.Idle;
    }

    /// <summary>
    /// Avança o windup do golpe (se estiver em <see cref="EnemyState.Attack"/>)
    /// e diz se já é hora de pedir o golpe de verdade, ou se ele já terminou.
    /// </summary>
    private (bool Terminou, bool PedirAgora) AtualizarPreparoDoGolpe(float delta)
    {
        if (_maquina.Estado != EnemyState.Attack)
            return (false, false);

        if (_golpeSolicitado)
            return (!(_contexto!.Combat?.IsAttacking ?? false), false);

        _windupRestante -= delta;
        if (_windupRestante > 0f)
            return (false, false);

        // O aviso apaga ao FIM do windup, não ao golpear de verdade: é o
        // instante em que a preparação vira o golpe em si.
        _contexto!.AttackTelegraph?.DesligarAviso();
        _golpeSolicitado = true;

        // O golpe em si é pedido por `IntentFrame.AttackPressed`, o MESMO
        // caminho do jogador (CharacterController já chama
        // `Combat.RequestBasicAttack()` sozinho ao ver isto) -- nenhum acesso
        // direto a CombatComponent precisa existir aqui para golpear.
        return (false, true);
    }

    private void AoTrocarDeEstado(EnemyState anterior, EnemyState novo)
    {
        if (novo == EnemyState.Attack)
        {
            _windupRestante = Definition.AttackWindup;
            _golpeSolicitado = false;
            _contexto?.AttackTelegraph?.LigarAviso();
        }
        else if (anterior == EnemyState.Attack)
        {
            _contexto?.AttackTelegraph?.DesligarAviso();
        }
    }

    private IntentFrame MontarIntencao(CharacterBody3D corpo, CharacterController alvo, bool pedirAtaqueAgora, float delta)
    {
        var move = Vector2.Zero;
        var pedirPulo = false;

        // Só anda perseguindo -- "para" ao alertar, ao atacar e ao descansar
        // é o próprio pedido do ticket 22 ("persegue, PARA ao chegar perto").
        if (_maquina.Estado == EnemyState.Chase)
        {
            _contexto!.NavigationMotor?.SetTarget(alvo.GlobalPosition, delta);
            var direcaoMundo = _contexto.NavigationMotor?.GetDesiredDirection(corpo.GlobalPosition) ?? Vector3.Zero;

            // Força de separação (spec 09 §4): sem isto, a horda que a
            // navegação já traz até perto do jogador vira uma bola de corpos
            // sobrepostos -- evitação de colisão sozinha não resolve, precisa
            // desta força leve por cima, somada à direção de perseguição
            // ANTES de normalizar para movimento (desvia a direção, não
            // troca a velocidade -- WorldToMovement normaliza de qualquer
            // jeito).
            direcaoMundo += CalcularForcaDeSeparacao(corpo.GlobalPosition);

            var yaw = _contexto!.Movement?.CameraReference?.YawDegrees ?? 45f;
            move = CameraMath.WorldToMovement(direcaoMundo, yaw);

            // "Sobem nos objetos escaláveis pelas ligações de navegação, sem
            // travar" (ticket 23): uma ligação de navegação é um caminho
            // GEOMÉTRICO válido, mas o corpo não sobe 1+ m parado no lugar
            // sozinho -- precisa de um pulo de verdade, o MESMO
            // `IntentFrame.JumpPressed` que o jogador usa (spec 09 §1, um
            // único sistema de locomoção). `JumpState.RequestJump` só
            // enfileira um pedido (consumido quando `CanJump`, com coyote
            // time) -- pedir de novo todo quadro enquanto ainda precisar não
            // duplica o pulo nem interrompe um já em andamento.
            pedirPulo = _contexto.NavigationMotor?.PrecisaPular(corpo.GlobalPosition) ?? false;
        }

        // Encara o alvo em qualquer estado que não seja Idle -- "vira para o
        // alvo" já vale desde o Alert, spec 09 §2. No ar (ticket 24) também
        // não: "não ataca nem recalcula rota" inclui não ficar se virando
        // para quem golpeou enquanto voa sem controle nenhum.
        var direcaoParaAlvo = new Vector3(
            alvo.GlobalPosition.X - corpo.GlobalPosition.X, 0f, alvo.GlobalPosition.Z - corpo.GlobalPosition.Z);
        var temMira = _maquina.Estado is not (EnemyState.Idle or EnemyState.Airborne)
            && direcaoParaAlvo.LengthSquared() > 0.0001f;

        return new IntentFrame(
            Move: move,
            ScreenPointer: Vector2.Zero,
            AimPoint: alvo.GlobalPosition,
            AimDirection: temMira ? direcaoParaAlvo.Normalized() : Vector3.Zero,
            HasAim: temMira,
            AttackPressed: pedirAtaqueAgora,
            AttackHeld: false,
            ConfirmPressed: false,
            CommandUpPressed: false,
            CommandDownPressed: false,
            CommandLeftPressed: false,
            CommandRightPressed: false,
            FormScrollDelta: 0,
            FormActivatePressed: false,
            JumpPressed: pedirPulo,
            DashPressed: false);
    }

    /// <summary>
    /// Empurrão para longe de outros inimigos ativos e próximos. Ver
    /// <see cref="SeparationMath"/> e spec 09 §4.
    /// </summary>
    /// <remarks>
    /// <see cref="Pool"/> é nulo para um inimigo que nunca passou por
    /// <c>EnemyPool.Acquire</c> (o probe de percepção/estado deste ticket 22
    /// instancia a cena direto) -- sem vizinhos conhecidos, força zero, nunca
    /// uma exceção.
    ///
    /// <see cref="_vizinhosBuffer"/> é reaproveitado entre quadros, nunca uma
    /// `List` nova por chamada -- convenções §5/spec 15 §3 proíbem alocação
    /// por quadro no hot path, e isto roda a cada <c>Poll</c> em `Chase`. Ver
    /// <see cref="EnemyPool.ObterPosicoesAtivas"/> para a outra metade da
    /// mesma disciplina (o pool também não aloca nem faz boxing ao iterar).
    /// </remarks>
    private Vector3 CalcularForcaDeSeparacao(Vector3 posicao)
    {
        if (Pool is null)
            return Vector3.Zero;

        Pool.ObterPosicoesAtivas(_contexto!.Owner, _vizinhosBuffer);

        return SeparationMath.ComputeForce(posicao, _vizinhosBuffer, RaioDeSeparacao, PesoDeSeparacao);
    }

    /// <remarks>
    /// Interrompe o que quer que estivesse fazendo -- "apanhar o interrompe;
    /// ele se recompõe e volta a perseguir", ticket 22. `Cancel()` fecha
    /// tanto um golpe já pedido (janela de acerto da arma) quanto um windup
    /// que nem chegou a pedir nada ainda (que não é `IsAttacking` para o
    /// `CombatComponent`, mas ainda precisa apagar o próprio aviso visual).
    ///
    /// "Lançamento vertical" (ticket 24) é lido do PRÓPRIO golpe, sem
    /// nenhum campo novo em <c>DamageInfo</c>: o anti-aéreo (Rising Slash/
    /// Uppercut) já golpeia com <c>Direction = Vector3.Up</c>, enquanto todo
    /// golpe comum (inclusive os do combo aéreo que SUSTENTA um inimigo já
    /// no ar) golpeia com uma direção majoritariamente horizontal -- o
    /// impulso vertical desses é aplicado à parte, via `ApplyKnockback`, sem
    /// aparecer aqui. `> 0.7`, não `> 0`: uma folga generosa contra qualquer
    /// direção só um pouco inclinada para cima continuar contando como
    /// golpe comum.
    /// </remarks>
    private void AoApanhar(DamageInfo golpe)
    {
        _maquina.RegistrarGolpeRecebido(lancamentoVertical: golpe.Direction.Y > 0.7f);
        _contexto?.AttackTelegraph?.DesligarAviso();
        _contexto?.Combat?.Cancel();
    }

    /// <remarks>
    /// <c>HealthState.Died</c> dispara no máximo uma vez por vida (é
    /// idempotente), então isto só roda de verdade uma vez por reciclagem.
    ///
    /// Desliga a colisão JÁ, não só ao devolver ao pool: o corpo ainda fica
    /// visível por <see cref="EnemyDefinition.DeathDuration"/> (a "animação de
    /// morte" -- sem modelo/`AnimationPlayer` de verdade ainda, ADR-010), mas
    /// não deveria continuar bloqueando passagem nem sendo alvo de raycast
    /// nenhum enquanto isso. `EnemyPool.Release`, ao fim do temporizador,
    /// zera de novo por garantia (cinto e suspensório), e é ele quem lembra
    /// os valores originais para restaurar na próxima <c>Acquire</c>.
    /// </remarks>
    private void AoMorrer(DamageInfo golpe)
    {
        _maquina.RegistrarMorte();
        _contexto?.AttackTelegraph?.DesligarAviso();
        _contexto?.Combat?.Cancel();

        _contexto!.Body.CollisionLayer = 0;
        _contexto.Body.CollisionMask = 0;

        // O chefe morreu: some da barra própria. Só limpa se AINDA sou eu lá
        // -- outro chefe pode já ter se anunciado depois de mim (onda
        // seguinte), e eu não deveria apagar a referência de outro inimigo.
        if (Definition.IsBoss && ReferenceEquals(ServiceLocator.Session.BossBody, _contexto.Owner))
            ServiceLocator.Session.BossBody = null;

        // Contagem de onda (ticket 27, spec 10 §4): por evento, nunca por
        // varredura de cena a cada quadro. Aqui, não em EnemyPool.Release --
        // Release só roda DEPOIS de DeathDuration inteiro (a "animação" de
        // morte), e o WaveDirector precisa saber do abate na hora, não
        // segundos depois.
        ServiceLocator.Events.RaiseEnemyKilled(new EnemyKilledEvent());

        _temporizadorDeMorte = 0f;
        _liberado = false;
    }
}
