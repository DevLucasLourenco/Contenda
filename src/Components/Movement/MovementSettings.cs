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
}
