using Godot;

namespace Contenda.Camera;

/// <summary>
/// A matemática do enquadramento 2.5D, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// Separada do <see cref="CameraRig"/> de propósito: é a parte que dá para errar
/// em silêncio — um sinal trocado deixa a câmera "funcionando", só que
/// enquadrando do lado oposto — e a única que dá para testar sem abrir a engine.
///
/// Ver docs/specs/02-camera-e-mundo-25d.md §3 e §8.
/// </remarks>
public static class CameraMath
{
    /// <summary>
    /// Para onde a câmera olha, dados os ângulos travados.
    /// </summary>
    /// <remarks>
    /// As rotações são compostas à mão em vez de por <c>Basis.FromEuler</c>
    /// porque a ordem importa e a convenção de ordem do Godot é fácil de
    /// confundir: aqui é inclinar primeiro (em torno do X local), girar depois
    /// (em torno do Y do mundo). Trocar a ordem inclina o horizonte.
    /// </remarks>
    public static Vector3 LookDirection(float pitchDegrees, float yawDegrees)
    {
        var yaw = new Basis(Vector3.Up, Mathf.DegToRad(yawDegrees));
        var pitch = new Basis(Vector3.Right, Mathf.DegToRad(pitchDegrees));
        return (yaw * pitch) * Vector3.Forward;
    }

    /// <summary>
    /// Deslocamento do alvo até a câmera. Somado à posição do alvo, dá onde a
    /// câmera fica.
    /// </summary>
    public static Vector3 OffsetFromAngles(float pitchDegrees, float yawDegrees, float distance)
        => -LookDirection(pitchDegrees, yawDegrees) * distance;

    /// <summary>
    /// Converte a entrada de WASD em direção no mundo, relativa à câmera.
    /// </summary>
    /// <remarks>
    /// **Requisito não negociável:** W move para cima na tela, em qualquer ponto
    /// da arena. Como o yaw é travado em 45°, "para cima na tela" é uma diagonal
    /// no mundo — e é por isso que a conversão não pode ser omitida.
    ///
    /// O yaw usado é sempre o do <see cref="CameraSettings"/>, nunca a rotação
    /// instantânea do nó: em tremor de tela ela oscila, e o movimento oscilaria
    /// junto.
    /// </remarks>
    /// <param name="input">Eixo bruto do WASD. Y negativo é frente.</param>
    /// <param name="yawDegrees">Giro da câmera, vindo dos ajustes.</param>
    public static Vector3 MovementToWorld(Vector2 input, float yawDegrees)
    {
        if (input.LengthSquared() <= 0f)
            return Vector3.Zero;

        var yaw = new Basis(Vector3.Up, Mathf.DegToRad(yawDegrees));
        return (yaw * new Vector3(input.X, 0f, input.Y)).Normalized();
    }

    /// <summary>
    /// Aproximação suave e criticamente amortecida, independente de framerate.
    /// </summary>
    /// <remarks>
    /// Existe para não usar <c>Lerp(atual, alvo, delta)</c>, que é a armadilha
    /// listada em docs/specs/15-qualidade-testes-e-performance.md §5: o
    /// resultado passa a depender de quantos quadros por segundo a máquina
    /// entrega, e a câmera fica mais "presa" em telas rápidas.
    ///
    /// Formulação clássica de mola criticamente amortecida, com a exponencial
    /// aproximada por série — mais barata que <c>Exp</c> e indistinguível na
    /// faixa de tempo que usamos.
    /// </remarks>
    public static float SmoothDamp(
        float current, float target, ref float velocity, float smoothTime, float delta)
    {
        if (delta <= 0f)
            return current;

        // Zero significa "sem suavização": vai direto, sem divisão por zero.
        if (smoothTime <= 0.0001f)
        {
            velocity = 0f;
            return target;
        }

        var omega = 2f / smoothTime;
        var x = omega * delta;
        var exp = 1f / (1f + x + (0.48f * x * x) + (0.235f * x * x * x));

        var diferenca = current - target;
        var temp = (velocity + (omega * diferenca)) * delta;
        velocity = (velocity - (omega * temp)) * exp;

        return target + ((diferenca + temp) * exp);
    }

    /// <summary>
    /// Suaviza no espaço, com tempos separados para o plano e para a altura.
    /// </summary>
    /// <remarks>
    /// A separação não é capricho: com um tempo só, a câmera acompanha cada pulo
    /// e o enquadramento balança. A altura precisa ser bem mais lenta —
    /// ver docs/specs/16-mobilidade-criticos-e-combate-aereo.md §3.
    /// </remarks>
    public static Vector3 SmoothDamp(
        Vector3 current, Vector3 target, ref Vector3 velocity,
        float horizontalSmoothTime, float verticalSmoothTime, float delta)
    {
        var vx = velocity.X;
        var vy = velocity.Y;
        var vz = velocity.Z;

        var x = SmoothDamp(current.X, target.X, ref vx, horizontalSmoothTime, delta);
        var y = SmoothDamp(current.Y, target.Y, ref vy, verticalSmoothTime, delta);
        var z = SmoothDamp(current.Z, target.Z, ref vz, horizontalSmoothTime, delta);

        velocity = new Vector3(vx, vy, vz);
        return new Vector3(x, y, z);
    }
}
