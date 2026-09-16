using Contenda.Camera;
using Contenda.Characters.Base;
using Contenda.Components.Combat;
using Contenda.Input;
using Godot;

namespace Contenda.Components.Movement;

/// <summary>
/// Locomoção. Consome intenção, não teclado.
/// </summary>
/// <remarks>
/// **Este componente é o mesmo para jogador e inimigo.** Ele nunca lê
/// <c>Input</c>: recebe um <see cref="IntentFrame"/>, venha ele do
/// <c>PlayerInputController</c> ou do <c>EnemyBrain</c> no M5. É o que evita
/// duplicar locomoção — e é por isso que melhorar o movimento do jogador melhora
/// o do inimigo de graça.
/// </remarks>
public sealed partial class MovementComponent : Node, ICharacterComponent
{
    /// <summary>Ajustes de locomoção. Vêm do <see cref="CharacterDefinition"/>.</summary>
    [Export] public MovementSettings Settings { get; set; } = new();

    /// <summary>
    /// De onde vem o giro da câmera, para o WASD ser relativo à tela.
    /// </summary>
    /// <remarks>
    /// O valor é lido a cada quadro, não cacheado: é uma leitura de propriedade,
    /// não um lookup, e cachear quebraria a edição do `.tres` em runtime que o
    /// <see cref="CameraSettings"/> promete.
    ///
    /// Usa-se o giro dos AJUSTES, nunca a rotação instantânea do nó da câmera:
    /// em tremor de tela ela oscila, e o movimento oscilaria junto.
    /// </remarks>
    [Export] public CameraSettings? CameraReference { get; set; }

    /// <summary>Em quanto tempo a repulsão de um golpe decai até zero, em segundos.</summary>
    /// <remarks>
    /// Duração fixa, não taxa fixa — ver ticket 11 e <see cref="KnockbackState"/>.
    /// Um golpe fraco e um forte decaem no MESMO tempo, só com magnitudes
    /// diferentes; a taxa antiga (m/s² constante) fazia golpes fortes
    /// empurrarem por mais tempo, o que a spec não pede.
    /// </remarks>
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float KnockbackDuration { get; set; } = 0.25f;

    /// <summary>Fonte usada para a trava de rotação que o próprio dash aplica em si mesmo.</summary>
    private const string DashLockSource = "movement.dash";

    private CharacterContext? _contexto;
    private KnockbackState _recuo = new(0.25f);
    private readonly JumpState _pulo = new();
    private readonly DashState _dash = new();
    private int _pulosNoArRestantes;
    private float _dashCooldownRestante;
    private bool _dashUsadoNoAr;

    /// <summary>Velocidade atual, para quem precisar consultar.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>
    /// Se o personagem está apoiado no chão, segundo o <c>Tick</c> mais
    /// recente.
    /// </summary>
    /// <remarks>
    /// Cacheado aqui em vez de quem precisar chamar `corpo.IsOnFloor()`
    /// direto: um `CharacterBody3D` que ainda não rodou nenhum
    /// `MoveAndSlide` (o primeiro quadro da própria existência) devolve
    /// falso ali -- e "acabei de nascer" é indistinguível de "estou caindo"
    /// para quem só olha de fora. Partir de <c>true</c> é o padrão mais
    /// seguro: um personagem parado no instante em que entra na árvore está
    /// infinitamente mais perto de "no chão" do que de "no ar". O ticket 19
    /// expôs isto: um probe pedindo um ataque no MESMO quadro em que o
    /// personagem nasceu via `CombatComponent.RequestBasicAttack` direto
    /// (sem passar pelo fluxo normal de intenção) via `IsOnFloor()` cru e
    /// concluía "no ar" por engano, roteando o golpe de solo para o combo
    /// aéreo.
    /// </remarks>
    public bool IsGrounded { get; private set; } = true;

    /// <summary>
    /// Apoiado no chão de verdade, sem a defasagem de um quadro que
    /// <see cref="IsGrounded"/> sozinho carrega bem no quadro de um impulso
    /// vertical nascendo agora.
    /// </summary>
    /// <remarks>
    /// <see cref="IsGrounded"/> reflete <c>IsOnFloor()</c> lido ANTES de somar
    /// o recuo deste quadro (ver <c>Tick</c>) -- então o quadro exato de um
    /// lançamento (recuo para cima aplicado por fora, via
    /// <see cref="ApplyKnockback"/>) ainda vê <see cref="IsGrounded"/> como
    /// verdadeiro. <see cref="Velocity"/>, por outro lado, já inclui esse
    /// recuo (lido DEPOIS do <c>MoveAndSlide</c> do próprio quadro), então
    /// "subindo" sempre desmente um <see cref="IsGrounded"/> que ainda não se
    /// atualizou. Mesma disciplina do <c>realmenteNoAr</c> que
    /// <see cref="AtualizarDash"/> já calcula por conta própria (com a
    /// velocidade ainda local, não commitada) -- esta property expõe o mesmo
    /// sinal para quem está FORA deste componente, como o <c>EnemyBrain</c>
    /// (ticket 24), que só enxerga o estado já commitado do quadro anterior.
    /// </remarks>
    public bool IsGroundedConfiavel => IsGrounded && Velocity.Y <= 0f;

    /// <summary>
    /// Multiplicador extra de gravidade, imposto de fora — o golpe aéreo e a
    /// estocada de queda (ticket 19, spec 16 §6) usam isto para "segurar" o
    /// personagem no ar ou acelerar a queda. 1 é neutro.
    /// </summary>
    /// <remarks>
    /// Property simples, sem decaimento próprio: quem pede precisa devolver a
    /// 1 quando acabar — <c>MeleeWeapon</c> escreve um valor definido (a
    /// redução, a aceleração ou 1) em TODO `Tick`, nunca só quando o efeito
    /// está ativo, para nunca deixar um valor preso de trás. Como este
    /// `Tick` roda ANTES de <c>CombatComponent.Tick</c> no mesmo quadro
    /// (spec 01 §6, <c>CharacterController</c>), o efeito só aparece 1
    /// quadro depois de pedido — imperceptível numa janela de vários
    /// quadros.
    /// </remarks>
    public float ExternalGravityScale { get; set; } = 1f;

    /// <summary>Se um dash está em andamento agora. Para o probe/depuração.</summary>
    public bool IsDashing => _dash.IsActive;

    /// <summary>Quanto falta para o dash recarregar, em segundos. Para o probe/depuração.</summary>
    public float DashCooldownRemaining => _dashCooldownRestante;

    public void Bind(CharacterContext contexto) => _contexto = contexto;

    /// <summary>
    /// Empurra o personagem, somando ao movimento próprio.
    /// </summary>
    /// <remarks>
    /// Decai sozinho, e não substitui a velocidade: substituir faria o alvo
    /// parar de andar ao apanhar, o que sob câmera fixa parece travamento.
    /// </remarks>
    public void ApplyKnockback(Vector3 impulso) => _recuo.Apply(impulso);

    public void Configure(CharacterDefinition definicao)
    {
        if (definicao.Movement is not null)
            Settings = definicao.Movement;

        ReiniciarEstadoTransiente();
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5 (ticket 25).</summary>
    /// <remarks>
    /// Não recebe <c>CharacterDefinition</c>: ao contrário de <c>Configure</c>,
    /// que troca <see cref="Settings"/> (chamado também na troca de arquétipo
    /// do ticket 12), a reciclagem do pool reaproveita a MESMA definição --
    /// só o estado transiente (recuo, pulo, dash) precisa zerar.
    /// </remarks>
    public void ResetForSpawn() => ReiniciarEstadoTransiente();

    /// <remarks>
    /// Compartilhado por <see cref="Configure"/> e <see cref="ResetForSpawn"/>:
    /// as duas situações (trocar de arquétipo; reciclar do pool) precisam
    /// zerar exatamente o mesmo estado transiente, e duplicar a lista aqui e
    /// ali é o tipo de coisa que diverge silenciosamente na próxima vez que
    /// alguém mexe só numa das duas.
    /// </remarks>
    private void ReiniciarEstadoTransiente()
    {
        // Reconstruído aqui, não no inicializador de campo: [Export] só
        // aplica KnockbackDuration depois que o campo já teria rodado com o
        // valor padrão do código, ignorando o que a cena pediu.
        _recuo = new KnockbackState(KnockbackDuration);

        // Reconfigurar (troca de arquétipo, tecla de debug do ticket 12) não
        // deveria deixar um dash em andamento ou uma recarga presa de trás.
        _pulo.Reset();
        _dash.Cancel();
        _pulosNoArRestantes = Settings.MaxAirJumps;
        _dashCooldownRestante = 0f;
        _dashUsadoNoAr = false;
        ExternalGravityScale = 1f;
        IsGrounded = true;

        // `Velocity` é só um espelho do `corpo.Velocity` de depois do último
        // `Tick` -- sem zerar os dois aqui, um inimigo reciclado herdaria o
        // impulso físico de um golpe da vida anterior por um quadro inteiro,
        // até o próprio `Tick` recalcular. Ticket 25: a reciclagem precisa
        // estar limpa ANTES do primeiro quadro pós-`Acquire`, não só a partir
        // do segundo.
        Velocity = Vector3.Zero;
        if (_contexto is not null)
            _contexto.Body.Velocity = Vector3.Zero;
    }

    /// <summary>
    /// Aplica uma intenção a este tique de física.
    /// </summary>
    /// <remarks>
    /// Chamado pelo contêiner, não pelo próprio <c>_PhysicsProcess</c>: quem
    /// decide a ordem dos componentes dentro do quadro é o
    /// <see cref="CharacterController"/>. Ver spec 01 §6.
    /// </remarks>
    public void Tick(in IntentFrame intencao, float delta)
    {
        if (_contexto is null)
            return;

        var corpo = _contexto.Body;
        var yaw = CameraReference?.YawDegrees ?? 45f;
        var travas = _contexto.Combat?.ActiveLocks ?? ActionLock.None;
        var noChao = corpo.IsOnFloor();
        IsGrounded = noChao;

        // Apoiado: recarrega os pulos extras no ar (zero no estado base) e
        // libera um dash novo no ar -- as duas coisas são "por pulo", não "por
        // recarga própria". Ver spec 16 §3-4.
        if (noChao)
        {
            _pulosNoArRestantes = Settings.MaxAirJumps;
            _dashUsadoNoAr = false;

            // Todo combo aéreo de verdade termina em aterrissagem: zerar o
            // teto de juggle a cada quadro parado no chão é mais simples que
            // detectar a borda exata de "acabou de aterrissar", e igualmente
            // correto -- ninguém é lançado de novo sem antes deixar de estar
            // no chão. Ticket 19, spec 16 §6.
            _contexto.Health?.ResetAerialJuggle();
        }

        _pulo.Advance(delta, noChao, Settings.CoyoteTime);
        if (intencao.JumpPressed)
            _pulo.RequestJump(Settings.JumpBufferTime);

        // WASD relativo à CÂMERA. Sem esta conversão, W andaria na diagonal do
        // mundo em vez de para cima na tela — requisito da spec 02 §8.
        var direcao = CameraMath.MovementToWorld(intencao.Move, yaw);

        // Movimento travado (ex.: golpe em andamento, ticket 08) ignora o WASD,
        // mas gravidade e repulsão continuam integrando: travar a própria
        // locomoção não deveria imunizar contra ser lançado por um golpe.
        var travado = (travas & ActionLock.Movement) != 0;
        var desejada = travado ? Vector3.Zero : direcao * Settings.MoveSpeed;

        // Controle no ar: mais lento para acelerar E para frear, nunca a
        // velocidade máxima em si -- "controle reduzido" é sobre redirecionar
        // no meio do salto, não sobre andar mais devagar no ar. Ver spec 16 §3.
        var aceleracao = Settings.Acceleration;
        var desaceleracao = Settings.Deceleration;
        if (!noChao)
        {
            aceleracao *= Settings.AirControlFactor;
            desaceleracao *= Settings.AirControlFactor;
        }

        var velocidade = MovementMath.Accelerate(corpo.Velocity, desejada, aceleracao, desaceleracao, delta);

        velocidade += _recuo.Current;
        _recuo.Advance(delta);

        // Pulo: o solo/coyote sempre pode; um extra no ar só se ainda sobrar
        // saldo (zero no estado base, formas futuras concedem mais).
        var podePular = _pulo.CanJump || _pulosNoArRestantes > 0;
        if (_pulo.HasBufferedJump && podePular)
        {
            if (!_pulo.CanJump)
                _pulosNoArRestantes--;

            // A velocidade de saída é atribuída direto, SEM passar pela
            // gravidade deste mesmo quadro -- o pulo é um evento instantâneo;
            // a gravidade retoma a partir do próximo quadro. Aplicá-la aqui
            // também comeria uma fração do impulso, silenciosamente.
            velocidade.Y = MovementMath.JumpVelocity(Settings.Gravity, Settings.JumpHeight);
            _pulo.Consume();
        }
        else
        {
            velocidade.Y = MovementMath.ApplyGravity(
                velocidade.Y, Settings.Gravity * ExternalGravityScale, Settings.FallGravityScale, noChao, delta);
        }

        AtualizarDash(intencao, direcao, travas, noChao, delta, ref velocidade);

        corpo.Velocity = velocidade;
        corpo.MoveAndSlide();
        Velocity = corpo.Velocity;

        // Relida, não a `travas` capturada no topo: um dash que começa NESTE
        // quadro aplica a própria trava de rotação dentro de AtualizarDash,
        // acima -- checar a variável antiga deixaria a primeira virada do
        // dash escapar por um quadro, porque a trava passaria a valer só a
        // partir do PRÓXIMO Tick.
        var travasAtuais = _contexto.Combat?.ActiveLocks ?? ActionLock.None;
        if ((travasAtuais & ActionLock.Rotation) == 0)
            Girar(intencao, direcao, delta);
    }

    /// <remarks>
    /// Pedido novo, avanço em andamento e recarga moram juntos aqui: os três
    /// mexem no mesmo relógio (<see cref="DashState"/>) e na mesma decisão de
    /// "pode começar agora", e separar em métodos menores só espalharia esse
    /// estado sem isolar nada que precise ser testado sozinho -- o `DashState`
    /// em si já é a peça extraída e testável.
    /// </remarks>
    private void AtualizarDash(in IntentFrame intencao, Vector3 direcaoDoMovimento, ActionLock travas, bool noChao, float delta, ref Vector3 velocidade)
    {
        _dashCooldownRestante = Mathf.Max(0f, _dashCooldownRestante - delta);

        // `noChao` sozinho não basta aqui: ele reflete o chão de ANTES deste
        // quadro mexer em qualquer coisa, então no quadro EXATO em que um
        // pulo dispara (`velocidade.Y` acabou de virar positivo, um pouco
        // acima) `noChao` ainda lê "apoiado" -- um dash pedido nesse mesmo
        // quadro veria `noChao=true` e nunca marcaria `_dashUsadoNoAr`,
        // liberando um segundo dash de graça no mesmo pulo. Somar a
        // velocidade vertical já decidida (pulo ou queda) fecha essa brecha
        // sem precisar reler `IsOnFloor()` depois do MoveAndSlide.
        //
        // A mesma defasagem existe no controle aéreo (`aceleracao`/
        // `desaceleracao` logo acima, que também ramificam em `noChao` cru):
        // no quadro exato do pulo, ele ainda usa a aceleração de solo por um
        // quadro só, e se autocorrige no seguinte. Sem consequência aqui
        // porque nada depende de contar quantas vezes já dashou no ar --
        // só o dash precisou do remendo.
        //
        // `> 0f`, não uma tolerância maior: os únicos dois valores que
        // `velocidade.Y` assume nesta função são -1 (apoiado, ver
        // MovementMath.ApplyGravity) e a velocidade de saída de um pulo
        // (sempre vários m/s) -- não há ruído de ponto flutuante por perto
        // para justificar uma margem.
        var realmenteNoAr = !noChao || velocidade.Y > 0f;

        // Bloqueado só por ActionLock.Abilities, nunca por Movement: o dash
        // PRECISA escapar de recovery de ataque básico e de hitstun (ambos
        // travam Movement, spec 16 §4), mas não deveria interromper uma
        // habilidade em execução (que trava Abilities além de Movement).
        // Atordoamento pesado ainda não existe como sistema -- fica para
        // quando o M5 introduzir um.
        var podeComecar = _dashCooldownRestante <= 0f
            && !_dash.IsActive
            && (!realmenteNoAr || !_dashUsadoNoAr)
            && (travas & ActionLock.Abilities) == 0;

        if (intencao.DashPressed && podeComecar)
        {
            var corpo = _contexto!.Body;
            var frenteDoCorpo = -corpo.GlobalTransform.Basis.Z;
            var direcaoDoDash = direcaoDoMovimento.LengthSquared() > 0.0001f
                ? direcaoDoMovimento.Normalized()
                : new Vector3(frenteDoCorpo.X, 0f, frenteDoCorpo.Z).Normalized();

            _dash.Start(direcaoDoDash, Settings.DashDistance, Settings.DashDuration);
            _dashCooldownRestante = Settings.DashCooldown;

            if (realmenteNoAr)
                _dashUsadoNoAr = true;

            // Trava só ROTAÇÃO: o dash quer dirigir X/Z sozinho (não dá para
            // curvar no meio, spec 16 §4), mas ActionLock.Movement pertence à
            // decisão de "ignorar o WASD" acima -- travá-lo aqui zeraria
            // `desejada` no PRÓXIMO quadro por engano, mesmo já sobrescrevendo
            // X/Z logo abaixo.
            _contexto.Combat?.ApplyLock(DashLockSource, ActionLock.Rotation, Settings.DashDuration);
            _contexto.Health?.GrantInvulnerability(Settings.DashInvulnerability);
        }

        if (!_dash.IsActive)
            return;

        var velocidadeDoDash = _dash.Velocity;
        velocidade.X = velocidadeDoDash.X;
        velocidade.Z = velocidadeDoDash.Z;
        _dash.Advance(delta);
    }

    private void Girar(in IntentFrame intencao, Vector3 direcaoDoMovimento, float delta)
    {
        var corpo = _contexto!.Body;

        var alvo = Settings.FaceMode == FaceMode.Aim && intencao.HasAim
            ? intencao.AimDirection
            : direcaoDoMovimento;

        if (alvo.LengthSquared() < 0.0001f)
            return;

        // A frente do personagem é −Z, convenção do Godot.
        var anguloAlvo = Mathf.Atan2(-alvo.X, -alvo.Z);
        var novo = MovementMath.RotateToward(
            corpo.Rotation.Y, anguloAlvo, Settings.RotationSpeed, delta);

        corpo.Rotation = new Vector3(0f, novo, 0f);
    }
}
