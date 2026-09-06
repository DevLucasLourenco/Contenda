using Godot;

namespace Contenda.Components.Movement;

/// <summary>Para onde o personagem vira enquanto se move.</summary>
public enum FaceMode
{
    /// <summary>Encara o cursor. Padrão — ver ADR-011.</summary>
    Aim = 0,

    /// <summary>Encara a direção do movimento.</summary>
    Movement = 1,
}

/// <summary>
/// Parâmetros de locomoção de um personagem.
/// </summary>
[GlobalClass]
public sealed partial class MovementSettings : Resource
{
    /// <summary>Velocidade máxima no plano, em metros por segundo.</summary>
    [Export(PropertyHint.Range, "1,15,0.1")] public float MoveSpeed { get; set; } = 5.5f;

    /// <summary>Quão rápido arranca, em m/s².</summary>
    [Export(PropertyHint.Range, "5,200,1")] public float Acceleration { get; set; } = 45f;

    /// <summary>
    /// Quão rápido freia, em m/s². Deve ser maior que a aceleração.
    /// </summary>
    /// <remarks>
    /// Frear igual a arrancar faz o personagem patinar ao soltar a tecla, e
    /// acertar um golpe corpo a corpo vira loteria.
    /// </remarks>
    [Export(PropertyHint.Range, "5,300,1")] public float Deceleration { get; set; } = 60f;

    /// <summary>Velocidade de giro, em radianos por segundo.</summary>
    [Export(PropertyHint.Range, "1,40,0.5")] public float RotationSpeed { get; set; } = 14f;

    /// <summary>Para onde virar: cursor ou direção do movimento.</summary>
    [Export] public FaceMode FaceMode { get; set; } = FaceMode.Aim;

    /// <summary>Gravidade, em m/s². Positiva; o sinal é aplicado na integração.</summary>
    [Export(PropertyHint.Range, "5,60,0.5")] public float Gravity { get; set; } = 22f;

    // --- pulo (ticket 17) ---------------------------------------------------

    /// <summary>
    /// Altura de pico do pulo, em metros.
    /// </summary>
    /// <remarks>
    /// A velocidade de saída é DERIVADA disto (<see cref="MovementMath.JumpVelocity"/>),
    /// nunca configurada direto: altura em metros é o que dá para visualizar
    /// olhando a cena; velocidade de saída não. Ver spec 16 §3.
    /// </remarks>
    [Export(PropertyHint.Range, "0.5,6,0.1")] public float JumpHeight { get; set; } = 2.2f;

    /// <summary>Fração da aceleração/desaceleração de solo mantida no ar.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float AirControlFactor { get; set; } = 0.65f;

    /// <summary>Tempo depois de sair de uma borda em que pular ainda funciona, em segundos.</summary>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float CoyoteTime { get; set; } = 0.12f;

    /// <summary>Tempo antes de aterrissar em que apertar pulo já conta, em segundos.</summary>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float JumpBufferTime { get; set; } = 0.12f;

    /// <summary>
    /// Quanto a gravidade multiplica durante a queda, em relação à subida.
    /// </summary>
    /// <remarks>
    /// Maior que 1: a queda é mais pesada que a subida, de propósito -- pulo
    /// com a mesma gravidade nos dois sentidos flutua, e sob câmera fixa isso
    /// é o erro mais perceptível de todos. Ver spec 16 §3.
    /// </remarks>
    [Export(PropertyHint.Range, "1,3,0.05")] public float FallGravityScale { get; set; } = 1.6f;

    /// <summary>Pulos extras no ar, além do primeiro. Zero no estado base — formas concedem mais.</summary>
    [Export(PropertyHint.Range, "0,3,1")] public int MaxAirJumps { get; set; }

    // --- dash (ticket 17, spec 16 §4) ---------------------------------------

    /// <summary>Distância percorrida pelo dash, em metros.</summary>
    [Export(PropertyHint.Range, "1,15,0.1")] public float DashDistance { get; set; } = 5.0f;

    /// <summary>Duração do dash, em segundos.</summary>
    [Export(PropertyHint.Range, "0.05,0.6,0.01")] public float DashDuration { get; set; } = 0.18f;

    /// <summary>Recarga do dash, em segundos. Não consome mana — só tempo.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float DashCooldown { get; set; } = 1.2f;

    /// <summary>Invulnerabilidade concedida do início do dash, em segundos.</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float DashInvulnerability { get; set; } = 0.12f;
}
