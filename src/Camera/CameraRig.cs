using Contenda.Characters.Base;
using Contenda.Components.Health;
using Godot;

namespace Contenda.Camera;

/// <summary>
/// Segue a posição do alvo. Nunca a rotação dele.
/// </summary>
/// <remarks>
/// **Este nó é IRMÃO do jogador na árvore, jamais filho.** Filho da cápsula, a
/// câmera herdaria a rotação do personagem e giraria a cada virada, a cada
/// golpe, a cada correção de mira — e o pilar do projeto é que o enquadramento
/// nunca muda. Ver docs/specs/02-camera-e-mundo-25d.md §4.
///
/// O acompanhamento roda em <c>_Process</c>, não em <c>_PhysicsProcess</c>:
/// é apresentação, e amarrá-lo à física produziria trepidação em telas de
/// atualização alta.
/// </remarks>
public sealed partial class CameraRig : Node3D
{
    /// <summary>Ajustes do enquadramento. Sem isto, valem os padrões da classe.</summary>
    [Export] public CameraSettings Settings { get; set; } = new();

    /// <summary>
    /// Caminho do que seguir, resolvido no <c>_Ready</c>.
    /// </summary>
    /// <remarks>
    /// Export de <c>NodePath</c>, e não de <c>Node3D</c> direto: exportar o tipo
    /// do nó parecia funcionar e **não vinculava** — a referência chegava nula em
    /// runtime, sem erro nenhum, e a câmera ficava parada na origem com os
    /// valores padrão. É o que as convenções §2 já mandavam usar.
    /// </remarks>
    [Export] public NodePath TargetPath { get; set; } = new();

    /// <summary>Caminho da câmera filha, resolvido no <c>_Ready</c>.</summary>
    [Export] public NodePath CameraPath { get; set; } = new();

    /// <summary>Intensidade do tremor ao ACERTAR um golpe. Ticket 11.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float HitLandedShakeIntensity { get; set; } = 0.15f;

    /// <summary>Intensidade do tremor ao LEVAR um golpe. Ticket 11.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float DamageTakenShakeIntensity { get; set; } = 0.3f;

    /// <summary>O que está sendo seguido. Trocável em runtime por <see cref="SetTarget"/>.</summary>
    public Node3D? Target { get; private set; }

    /// <summary>A câmera controlada por este rig.</summary>
    public CombatCamera? Camera { get; private set; }

    /// <summary>
    /// De quem este rig ouve acertos/golpes para sacudir a câmera.
    /// </summary>
    /// <remarks>
    /// Só existe quando <see cref="Target"/> é um <see cref="CharacterController"/>
    /// com <c>Context</c> já montado — o rig não sacode por qualquer coisa que
    /// esteja seguindo (uma câmera de replay futura poderia seguir um ponto
    /// sem personagem nenhum).
    /// </remarks>
    private CharacterController? _fonteDoTremor;

    /// <summary>
    /// Posição sem tremor.
    /// </summary>
    /// <remarks>
    /// O tremor é mantido separado de propósito. Somá-lo à posição seguida e
    /// depois reler <c>GlobalPosition</c> no quadro seguinte faria o resíduo de
    /// cada sacudida deslocar o rig **permanentemente** — a câmera iria
    /// derivando a cada golpe até desenquadrar.
    /// </remarks>
    private Vector3 _posicaoLimpa;

    private Vector3 _velocidade;
    private Vector3 _tremor;
    private float _alturaAlvo;
    private bool _iniciado;

    /// <summary>Deslocamento de tremor deste quadro. Para o probe/depuração.</summary>
    public Vector3 ShakeOffset => _tremor;

    public override void _Ready()
    {
        var ajustes = Settings ?? new CameraSettings();
        Settings = ajustes;

        Target = GetNodeOrNull<Node3D>(TargetPath);
        Camera = GetNodeOrNull<CombatCamera>(CameraPath);

        if (Camera is null)
        {
            // Falhar alto: sem câmera o jogo renderiza de um ponto arbitrário e
            // parece "quase certo", que é o pior modo de errar.
            GD.PushError($"{Name}: CameraPath não resolveu ('{CameraPath}').");
            return;
        }

        Camera.Configure(ajustes);

        if (Target is null)
            GD.PushWarning($"{Name}: TargetPath não resolveu ('{TargetPath}'); a câmera ficará parada.");

        Reenquadrar();

        // Adiado: se o alvo é um CharacterController, o próprio _Ready dele
        // (que monta Context) pode não ter rodado ainda -- rig e personagem
        // são IRMÃOS na árvore, e ordem entre irmãos não é garantida.
        CallDeferred(nameof(ConectarAoAlvo));
    }

    /// <summary>Troca o alvo seguido, sem solavanco.</summary>
    public void SetTarget(Node3D? alvo)
    {
        Target = alvo;
        Reenquadrar();
        ConectarAoAlvo();
    }

    public override void _ExitTree() => DesconectarDoAlvo();

    /// <summary>
    /// Sacode a câmera. Some sozinho por <c>ShakeDecay</c>.
    /// </summary>
    /// <remarks>
    /// O deslocamento é gerado no espaço da CÂMERA e só depois levado para o
    /// mundo: sacudir nos eixos do mundo, sob um yaw de 45°, faria o tremor sair
    /// na diagonal da tela em vez de para os lados.
    ///
    /// <see cref="CameraSettings.ShakeIntensityMultiplier"/> é aplicado AQUI,
    /// não em cada chamador — é o knob único que o menu de acessibilidade do
    /// M7 vai expor, e todo tremor (acerto, dano, o que vier depois) passa por
    /// ele automaticamente. Nunca mexe na rotação base da câmera — só desloca
    /// a posição, por cima do enquadramento normal.
    /// </remarks>
    public void Shake(float intensidade)
    {
        var escalada = intensidade * Settings.ShakeIntensityMultiplier;
        if (escalada <= 0f)
            return;

        var local = new Vector3(
            (float)GD.RandRange(-escalada, escalada),
            (float)GD.RandRange(-escalada, escalada),
            0f);

        var baseCamera = new Basis(Vector3.Up, Mathf.DegToRad(Settings.YawDegrees))
                         * new Basis(Vector3.Right, Mathf.DegToRad(Settings.PitchDegrees));

        _tremor += baseCamera * local;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        if (Target is not null)
        {
            if (!_iniciado)
                Reenquadrar();

            var desejada = PosicaoDesejada(Target.GlobalPosition);

            // Zona morta no plano: com o alvo parado, oscilações minúsculas de
            // posição viram tremor visível sob ângulo travado. Zerar a
            // velocidade junto é essencial — sem isso a suavização continua
            // integrando a velocidade guardada contra um alvo redefinido a cada
            // quadro, e a câmera desliza sozinha dentro da zona.
            var planoAtual = new Vector2(_posicaoLimpa.X, _posicaoLimpa.Z);
            var planoDesejado = new Vector2(desejada.X, desejada.Z);
            if (planoAtual.DistanceTo(planoDesejado) < Settings.DeadZoneRadius)
            {
                desejada = new Vector3(_posicaoLimpa.X, desejada.Y, _posicaoLimpa.Z);
                _velocidade = new Vector3(0f, _velocidade.Y, 0f);
            }

            _posicaoLimpa = CameraMath.SmoothDamp(
                _posicaoLimpa, desejada, ref _velocidade,
                Settings.FollowSmoothTime, Settings.VerticalFollowSmoothTime, dt);
        }

        if (_tremor.LengthSquared() > 0.000001f)
            _tremor = _tremor.Lerp(Vector3.Zero, Mathf.Min(1f, Settings.ShakeDecay * dt));
        else
            _tremor = Vector3.Zero;

        GlobalPosition = _posicaoLimpa + _tremor;
    }

    /// <summary>
    /// Coloca a câmera já enquadrando, sem a corrida inicial desde a origem.
    /// </summary>
    private void Reenquadrar()
    {
        _velocidade = Vector3.Zero;
        _tremor = Vector3.Zero;

        if (Target is null)
        {
            _iniciado = false;
            return;
        }

        var alvo = Target.GlobalPosition;
        _alturaAlvo = alvo.Y;
        _posicaoLimpa = PosicaoDesejada(alvo);
        GlobalPosition = _posicaoLimpa;
        _iniciado = true;
    }

    /// <summary>
    /// Onde a câmera deveria estar para este alvo.
    /// </summary>
    /// <remarks>
    /// Tem efeito colateral deliberado: atualiza o patamar de altura. A altura
    /// só muda quando o alvo se afasta o bastante em Y, para que um pulo caiba
    /// dentro da zona morta e não mova a câmera.
    /// </remarks>
    private Vector3 PosicaoDesejada(Vector3 alvo)
    {
        if (Mathf.Abs(alvo.Y - _alturaAlvo) > Settings.VerticalDeadZone)
            _alturaAlvo = alvo.Y;

        return new Vector3(alvo.X, _alturaAlvo, alvo.Z) + Settings.TargetOffset;
    }

    /// <remarks>
    /// Comunicação DIRETA, não pelo <c>GameEvents</c>: o rig já segura uma
    /// referência estrutural ao alvo (<see cref="Target"/>), e a spec 01 §5
    /// reserva o barramento para sistemas SEM relação — aqui a relação já
    /// existe. Ver o comentário da classe <c>GameEvents</c>.
    /// </remarks>
    private void ConectarAoAlvo()
    {
        DesconectarDoAlvo();

        if (Target is not CharacterController jogador || jogador.Context is not { } ctx)
            return;

        _fonteDoTremor = jogador;

        if (ctx.Combat is not null)
            ctx.Combat.HitLanded += AoAcertar;

        if (ctx.Health is not null)
            ctx.Health.Damaged += AoApanhar;
    }

    private void DesconectarDoAlvo()
    {
        if (_fonteDoTremor?.Context is { } ctx)
        {
            if (ctx.Combat is not null)
                ctx.Combat.HitLanded -= AoAcertar;

            if (ctx.Health is not null)
                ctx.Health.Damaged -= AoApanhar;
        }

        _fonteDoTremor = null;
    }

    private void AoAcertar(Node3D alvo) => Shake(HitLandedShakeIntensity);

    private void AoApanhar(DamageInfo golpe) => Shake(DamageTakenShakeIntensity);
}
