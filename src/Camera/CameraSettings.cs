using Godot;

namespace Contenda.Camera;

/// <summary>
/// Os números que definem a identidade visual do jogo.
/// </summary>
/// <remarks>
/// Editável em <c>data/camera/combat_camera.tres</c> e relido em runtime: ajustar
/// o enquadramento não deve exigir recompilar, porque encontrar o ângulo certo é
/// tentativa e erro.
///
/// Ver docs/specs/02-camera-e-mundo-25d.md §3 e §5.
/// </remarks>
[GlobalClass]
public sealed partial class CameraSettings : Resource
{
    /// <summary>Campo de visão, em graus. Baixo comprime a perspectiva — é o que dá o "2.5D".</summary>
    [Export(PropertyHint.Range, "20,70,0.5")] public float Fov { get; set; } = 38f;

    /// <summary>Inclinação. Negativo olha para baixo.</summary>
    [Export(PropertyHint.Range, "-70,-25,0.5")] public float PitchDegrees { get; set; } = -45f;

    /// <summary>Giro em torno do eixo vertical. TRAVADO em runtime.</summary>
    [Export(PropertyHint.Range, "0,90,0.5")] public float YawDegrees { get; set; } = 45f;

    /// <summary>Distância da câmera ao alvo, em metros.</summary>
    [Export(PropertyHint.Range, "5,30,0.5")] public float Distance { get; set; } = 17f;

    /// <summary>Plano de corte próximo, em metros.</summary>
    [Export(PropertyHint.Range, "0.05,2,0.05")] public float NearPlane { get; set; } = 0.5f;

    /// <summary>Plano de corte distante, em metros. Bem menor que o padrão da engine.</summary>
    [Export(PropertyHint.Range, "50,500,10")] public float FarPlane { get; set; } = 120f;

    /// <summary>Deslocamento do ponto seguido. Mira no torso, não nos pés.</summary>
    [Export] public Vector3 TargetOffset { get; set; } = new(0f, 1.0f, 0f);

    /// <summary>Suavização do acompanhamento no plano X/Z, em segundos.</summary>
    [Export(PropertyHint.Range, "0.02,0.6,0.01")] public float FollowSmoothTime { get; set; } = 0.16f;

    /// <summary>
    /// Suavização da altura, em segundos. Deliberadamente muito maior que a
    /// horizontal.
    /// </summary>
    /// <remarks>
    /// Seguir a altura do jogador com a mesma suavização de X e Z faz a câmera
    /// saltar a cada pulo, e sob ângulo travado isso enjoa em minutos. Ver
    /// docs/specs/16-mobilidade-criticos-e-combate-aereo.md §3.
    /// </remarks>
    [Export(PropertyHint.Range, "0.1,1.5,0.01")] public float VerticalFollowSmoothTime { get; set; } = 0.55f;

    /// <summary>Variação de altura ignorada, em metros. Absorve o pulo sem mexer na câmera.</summary>
    [Export(PropertyHint.Range, "0,4,0.1")] public float VerticalDeadZone { get; set; } = 2.4f;

    /// <summary>Raio morto no plano, em metros. Evita tremor com o alvo parado.</summary>
    [Export(PropertyHint.Range, "0,2,0.05")] public float DeadZoneRadius { get; set; } = 0.35f;

    /// <summary>Quanto a câmera antecipa na direção da mira. Zero desliga.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float LookAheadFactor { get; set; }

    /// <summary>Teto da antecipação, em metros.</summary>
    [Export(PropertyHint.Range, "0,8,0.1")] public float MaxLookAhead { get; set; } = 2.5f;

    /// <summary>Velocidade com que o tremor de tela decai.</summary>
    [Export(PropertyHint.Range, "1,20,0.5")] public float ShakeDecay { get; set; } = 6f;

    /// <summary>
    /// Experimento pós-MVP. Projeção ortográfica achata a profundidade e empurra
    /// o resultado para "isométrico chapado" — o oposto do pedido.
    /// </summary>
    [Export] public bool UseOrthogonal { get; set; }

    /// <summary>Altura enquadrada quando ortográfica, em metros.</summary>
    [Export(PropertyHint.Range, "5,40,0.5")] public float OrthogonalSize { get; set; } = 16f;
}
