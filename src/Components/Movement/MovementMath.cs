using Godot;

namespace Contenda.Components.Movement;

/// <summary>
/// Integração de velocidade e rotação, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// Separada do <see cref="MovementComponent"/> para ser testável fora da engine.
/// Ver docs/specs/15-qualidade-testes-e-performance.md §1.
/// </remarks>
public static class MovementMath
{
    /// <summary>
    /// Aproxima a velocidade horizontal da desejada.
    /// </summary>
    /// <remarks>
    /// Acelerar e frear usam taxas diferentes de propósito: soltar o WASD tem que
    /// parar mais rápido do que arrancar, senão o personagem patina e acertar um
    /// golpe corpo a corpo vira loteria.
    ///
    /// A componente Y é preservada intocada — a gravidade é integrada à parte, e
    /// misturar as duas faz o personagem escorregar durante a queda.
    /// </remarks>
    public static Vector3 Accelerate(
        Vector3 atual, Vector3 desejada, float aceleracao, float desaceleracao, float delta)
    {
        var alturaPreservada = atual.Y;

        var plano = new Vector3(atual.X, 0f, atual.Z);
        var alvo = new Vector3(desejada.X, 0f, desejada.Z);

        var taxa = alvo.LengthSquared() > 0f ? aceleracao : desaceleracao;
        var novo = plano.MoveToward(alvo, taxa * delta);

        return new Vector3(novo.X, alturaPreservada, novo.Z);
    }

    /// <summary>
    /// Gira em direção ao ângulo alvo pelo caminho mais curto.
    /// </summary>
    /// <remarks>
    /// Usa <c>AngleDifference</c> para não dar a volta longa ao cruzar ±180° — o
    /// sintoma, sem isso, é o personagem girando quase uma volta inteira ao virar
    /// para trás.
    /// </remarks>
    public static float RotateToward(float atual, float alvo, float velocidade, float delta)
    {
        var diferenca = Mathf.AngleDifference(atual, alvo);
        var passo = velocidade * delta;

        if (Mathf.Abs(diferenca) <= passo)
            return alvo;

        return atual + (Mathf.Sign(diferenca) * passo);
    }

    /// <summary>
    /// Integra a gravidade, mantendo o personagem colado ao chão quando apoiado.
    /// </summary>
    /// <remarks>
    /// No chão a velocidade vertical é zerada para um valor pequeno e negativo em
    /// vez de zero: zerada de fato, o <c>MoveAndSlide</c> perde o contato em
    /// rampas e o personagem passa a alternar entre apoiado e no ar, o que faz o
    /// acompanhamento da câmera tremer.
    /// </remarks>
    public static float ApplyGravity(float velocidadeY, float gravidade, bool noChao, float delta)
        => noChao && velocidadeY <= 0f
            ? -1f
            : velocidadeY - (gravidade * delta);
}
