using Contenda.Camera;
using Contenda.Characters.Base;
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

    /// <summary>Quão rápido a repulsão de um golpe se dissipa, em m/s².</summary>
    [Export(PropertyHint.Range, "5,120,1")] public float KnockbackDecay { get; set; } = 40f;

    private CharacterContext? _contexto;
    private Vector3 _repulsao;

    /// <summary>Velocidade atual, para quem precisar consultar.</summary>
    public Vector3 Velocity { get; private set; }

    public void Bind(CharacterContext contexto) => _contexto = contexto;

    /// <summary>
    /// Empurra o personagem, somando ao movimento próprio.
    /// </summary>
    /// <remarks>
    /// Decai sozinho, e não substitui a velocidade: substituir faria o alvo
    /// parar de andar ao apanhar, o que sob câmera fixa parece travamento.
    /// </remarks>
    public void ApplyKnockback(Vector3 impulso) => _repulsao += impulso;

    public void Configure(CharacterDefinition definicao)
    {
        if (definicao.Movement is not null)
            Settings = definicao.Movement;
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

        // WASD relativo à CÂMERA. Sem esta conversão, W andaria na diagonal do
        // mundo em vez de para cima na tela — requisito da spec 02 §8.
        var direcao = CameraMath.MovementToWorld(intencao.Move, yaw);
        var desejada = direcao * Settings.MoveSpeed;

        var velocidade = MovementMath.Accelerate(
            corpo.Velocity, desejada, Settings.Acceleration, Settings.Deceleration, delta);

        velocidade += _repulsao;
        _repulsao = _repulsao.MoveToward(Vector3.Zero, KnockbackDecay * delta);

        // A altura vem de `velocidade`, e não de `corpo.Velocity`: são iguais
        // hoje, mas quando o pulo entrar (ticket 17) o primeiro passo a mexer em
        // Y faria a gravidade integrar um valor velho, em silêncio.
        velocidade.Y = MovementMath.ApplyGravity(
            velocidade.Y, Settings.Gravity, corpo.IsOnFloor(), delta);

        corpo.Velocity = velocidade;
        corpo.MoveAndSlide();
        Velocity = corpo.Velocity;

        Girar(intencao, direcao, delta);
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
