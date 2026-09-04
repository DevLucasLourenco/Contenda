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

    /// <summary>O que seguir. Pode ser definido no editor ou em runtime.</summary>
    [Export] public Node3D? Target { get; set; }

    /// <summary>A câmera filha.</summary>
    [Export] public CombatCamera? Camera { get; set; }

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

    public override void _Ready()
    {
        Settings ??= new CameraSettings();
        Camera?.Configure(Settings);
        Reenquadrar();
    }

    /// <summary>Troca o alvo seguido, sem solavanco.</summary>
    public void SetTarget(Node3D? alvo)
    {
        Target = alvo;
        Reenquadrar();
    }

    /// <summary>
    /// Sacode a câmera. Some sozinho por <c>ShakeDecay</c>.
    /// </summary>
    /// <remarks>
    /// O deslocamento é gerado no espaço da CÂMERA e só depois levado para o
    /// mundo: sacudir nos eixos do mundo, sob um yaw de 45°, faria o tremor sair
    /// na diagonal da tela em vez de para os lados.
    /// </remarks>
    public void Shake(float intensidade)
    {
        var local = new Vector3(
            (float)GD.RandRange(-intensidade, intensidade),
            (float)GD.RandRange(-intensidade, intensidade),
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
}
