using Godot;

namespace Contenda.Components.Movement;

/// <summary>
/// Coyote time e jump buffer — quando um pulo pedido pode realmente começar.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>ActionLockSet</c>: as duas janelas de tempo
/// (borda de saída do chão, antecipação do próximo pulo) são exatamente onde
/// um erro de comparação passa despercebido, e testar isso exige controlar o
/// relógio — só dá para fazer fora da engine. Ver spec 16 §3 e o ticket 17.
/// </remarks>
public sealed class JumpState
{
    private float _coyoteRestante;
    private float _bufferRestante;

    /// <summary>Se um pulo pode começar agora, contando a borda de coyote time.</summary>
    public bool CanJump => _coyoteRestante > 0f;

    /// <summary>Se há um pedido de pulo recente, ainda não consumido.</summary>
    public bool HasBufferedJump => _bufferRestante > 0f;

    /// <summary>
    /// Envelhece as duas janelas pelo quadro.
    /// </summary>
    /// <param name="delta">Tempo do quadro, em segundos.</param>
    /// <param name="noChao">Se o personagem está apoiado agora.</param>
    /// <param name="coyoteTime">
    /// Duração da janela de coyote, renovada TODO quadro apoiado -- não é um
    /// contador que começa só ao sair da borda, é a distância até "há quanto
    /// tempo eu estava no chão" ficar grande demais.
    /// </param>
    public void Advance(float delta, bool noChao, float coyoteTime)
    {
        _coyoteRestante = noChao ? coyoteTime : Mathf.Max(0f, _coyoteRestante - delta);
        _bufferRestante = Mathf.Max(0f, _bufferRestante - delta);
    }

    /// <summary>Registra um pedido de pulo. Chamado na borda de subida da tecla.</summary>
    public void RequestJump(float jumpBufferTime) => _bufferRestante = jumpBufferTime;

    /// <summary>
    /// Consome o pulo pedido e a janela de coyote, para não pular duas vezes
    /// com o mesmo pedido nem no mesmo instante em que aterrissou.
    /// </summary>
    public void Consume()
    {
        _bufferRestante = 0f;
        _coyoteRestante = 0f;
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void Reset()
    {
        _coyoteRestante = 0f;
        _bufferRestante = 0f;
    }
}
