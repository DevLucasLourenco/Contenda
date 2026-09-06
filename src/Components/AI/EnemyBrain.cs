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

    private float _windupRestante;
    private bool _golpeSolicitado;

    /// <summary>Em qual fase da percepção/combate este inimigo está. Para o probe/depuração.</summary>
    public EnemyState Estado => _maquina.Estado;

    public void Bind(CharacterContext contexto)
    {
        if (_contexto?.Health is not null)
            _contexto.Health.Damaged -= AoApanhar;

        _contexto = contexto;

        if (_contexto.Health is not null)
            _contexto.Health.Damaged += AoApanhar;

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
        _contexto?.AttackTelegraph?.DesligarAviso();
    }

    public override void _ExitTree()
    {
        if (_contexto?.Health is not null)
            _contexto.Health.Damaged -= AoApanhar;
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        _maquina = new EnemyStateMachine(
            Definition.AlertDuration, Definition.AttackCooldown, Definition.StaggerDuration, Definition.LoseTargetDelay);
        _windupRestante = 0f;
        _golpeSolicitado = false;
        _contexto?.AttackTelegraph?.DesligarAviso();
        _contexto?.Combat?.Cancel();
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
        var alvo = ServiceLocator.Session.PlayerBody;
        if (alvo is null || !GodotObject.IsInstanceValid(alvo) || alvo.Context?.Health is not { IsAlive: true })
            return IntentFrame.Idle;

        var corpo = _contexto!.Body;
        var origemVisao = corpo.GlobalPosition + (Vector3.Up * EyeHeight);
        var alvoVisao = alvo.GlobalPosition + (Vector3.Up * EyeHeight);
        var distancia = corpo.GlobalPosition.DistanceTo(alvo.GlobalPosition);

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
        var (ataqueTerminou, pedirAtaqueAgora) = AtualizarPreparoDoGolpe(delta);

        var estadoAntes = _maquina.Estado;
        _maquina.Advance(delta, alvoVisivel, dentroDoAlcance, ataqueTerminou);

        if (_maquina.Estado != estadoAntes)
            AoTrocarDeEstado(estadoAntes, _maquina.Estado);

        return MontarIntencao(corpo, alvo, pedirAtaqueAgora);
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

    private IntentFrame MontarIntencao(CharacterBody3D corpo, CharacterController alvo, bool pedirAtaqueAgora)
    {
        var move = Vector2.Zero;

        // Só anda perseguindo -- "para" ao alertar, ao atacar e ao descansar
        // é o próprio pedido do ticket 22 ("persegue, PARA ao chegar perto").
        if (_maquina.Estado == EnemyState.Chase)
        {
            _contexto!.NavigationMotor?.SetTarget(alvo.GlobalPosition);
            var direcaoMundo = _contexto.NavigationMotor?.GetDesiredDirection(corpo.GlobalPosition) ?? Vector3.Zero;

            var yaw = _contexto!.Movement?.CameraReference?.YawDegrees ?? 45f;
            move = CameraMath.WorldToMovement(direcaoMundo, yaw);
        }

        // Encara o alvo em qualquer estado que não seja Idle -- "vira para o
        // alvo" já vale desde o Alert, spec 09 §2.
        var direcaoParaAlvo = new Vector3(
            alvo.GlobalPosition.X - corpo.GlobalPosition.X, 0f, alvo.GlobalPosition.Z - corpo.GlobalPosition.Z);
        var temMira = _maquina.Estado != EnemyState.Idle && direcaoParaAlvo.LengthSquared() > 0.0001f;

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
            JumpPressed: false,
            DashPressed: false);
    }

    /// <remarks>
    /// Interrompe o que quer que estivesse fazendo -- "apanhar o interrompe;
    /// ele se recompõe e volta a perseguir", ticket 22. `Cancel()` fecha
    /// tanto um golpe já pedido (janela de acerto da arma) quanto um windup
    /// que nem chegou a pedir nada ainda (que não é `IsAttacking` para o
    /// `CombatComponent`, mas ainda precisa apagar o próprio aviso visual).
    /// </remarks>
    private void AoApanhar(DamageInfo golpe)
    {
        _maquina.RegistrarGolpeRecebido();
        _contexto?.AttackTelegraph?.DesligarAviso();
        _contexto?.Combat?.Cancel();
    }
}
