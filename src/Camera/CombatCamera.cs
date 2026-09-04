using System;
using Godot;

namespace Contenda.Camera;

/// <summary>
/// A câmera em si. Aplica ângulos, distância e projeção a partir do
/// <see cref="CameraSettings"/>.
/// </summary>
/// <remarks>
/// Ela não decide para onde olhar — quem segue o alvo é o <see cref="CameraRig"/>,
/// que é seu pai. Esta classe só se posiciona em relação a ele e nunca muda de
/// rotação depois do <c>_Ready</c>.
///
/// A rotação é reaplicada a cada quadro por escolha: assim, mexer no
/// <c>.tres</c> com o jogo rodando reflete na hora, que é o que torna a busca
/// pelo ângulo certo praticável.
/// </remarks>
public sealed partial class CombatCamera : Camera3D
{
    private CameraSettings _settings = new();

    /// <summary>Aplica um conjunto de ajustes. Chamado pelo rig no <c>_Ready</c>.</summary>
    public void Configure(CameraSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        Apply();
    }

    public override void _Ready() => Apply();

    /// <remarks>
    /// Reaplicar a cada quadro existe só para que editar o <c>.tres</c> com o
    /// jogo rodando reflita na hora — é o que torna a busca pelo ângulo certo
    /// praticável. Em build de release não há editor para recarregar o recurso,
    /// então o custo de interop por quadro seria puro desperdício.
    /// </remarks>
    public override void _Process(double delta)
    {
        if (OS.IsDebugBuild())
            Apply();
    }

    private void Apply()
    {
        Fov = _settings.Fov;
        Near = _settings.NearPlane;
        Far = _settings.FarPlane;
        Projection = _settings.UseOrthogonal
            ? ProjectionType.Orthogonal
            : ProjectionType.Perspective;
        Size = _settings.OrthogonalSize;

        // Posição e rotação em espaço LOCAL: o pai (o rig) carrega a posição do
        // alvo, e a câmera fica sempre no mesmo deslocamento em relação a ele.
        // É isso que garante que girar o personagem não mexa no enquadramento.
        Position = CameraMath.OffsetFromAngles(
            _settings.PitchDegrees, _settings.YawDegrees, _settings.Distance);

        Rotation = new Vector3(
            Mathf.DegToRad(_settings.PitchDegrees),
            Mathf.DegToRad(_settings.YawDegrees),
            0f);
    }
}
