using Contenda.Components.Movement;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Integração de velocidade e rotação do personagem.
/// </summary>
/// <remarks>
/// A aceleração e a desaceleração são separadas de propósito: soltar o WASD tem
/// que frear mais rápido do que arrancar, senão o personagem patina e o combate
/// corpo a corpo fica impreciso.
/// </remarks>
public sealed class MovementMathTests
{
    private const float Tol = 0.01f;
    private const float Passo = 1f / 60f;

    [Fact]
    public void Parado_com_entrada_acelera_ate_a_velocidade_alvo()
    {
        var v = Vector3.Zero;
        var alvo = new Vector3(5.5f, 0f, 0f);

        for (var i = 0; i < 120; i++)
            v = MovementMath.Accelerate(v, alvo, 45f, 60f, Passo);

        Assert.Equal(5.5f, v.Length(), Tol);
    }

    [Fact]
    public void Sem_entrada_desacelera_ate_parar_de_verdade()
    {
        var v = new Vector3(5.5f, 0f, 0f);

        for (var i = 0; i < 120; i++)
            v = MovementMath.Accelerate(v, Vector3.Zero, 45f, 60f, Passo);

        // Tem que chegar a zero exato, não a um resíduo: velocidade residual faz
        // o personagem deslizar para sempre depois de soltar a tecla.
        Assert.Equal(Vector3.Zero, v);
    }

    [Fact]
    public void Frear_e_mais_rapido_que_arrancar()
    {
        var arrancando = Vector3.Zero;
        var freando = new Vector3(5.5f, 0f, 0f);
        var alvo = new Vector3(5.5f, 0f, 0f);

        for (var i = 0; i < 6; i++)
        {
            arrancando = MovementMath.Accelerate(arrancando, alvo, 45f, 60f, Passo);
            freando = MovementMath.Accelerate(freando, Vector3.Zero, 45f, 60f, Passo);
        }

        var percorridoArrancando = arrancando.Length();
        var percorridoFreando = 5.5f - freando.Length();
        Assert.True(percorridoFreando > percorridoArrancando,
            "a desaceleração deveria ser mais forte que a aceleração");
    }

    [Fact]
    public void Nunca_ultrapassa_a_velocidade_alvo()
    {
        var v = Vector3.Zero;
        var alvo = new Vector3(5.5f, 0f, 0f);

        for (var i = 0; i < 600; i++)
        {
            v = MovementMath.Accelerate(v, alvo, 200f, 200f, Passo);
            Assert.True(v.Length() <= 5.5f + Tol, $"ultrapassou no passo {i}: {v.Length()}");
        }
    }

    [Fact]
    public void A_altura_nao_e_tocada_pela_aceleracao_horizontal()
    {
        // A gravidade é integrada separado; misturar as duas faz o personagem
        // "escorregar" na queda.
        var v = new Vector3(0f, -9f, 0f);
        v = MovementMath.Accelerate(v, new Vector3(5f, 0f, 0f), 45f, 60f, Passo);
        Assert.Equal(-9f, v.Y, Tol);
    }

    [Theory]
    [InlineData(0f, 90f)]
    [InlineData(170f, -170f)]   // atravessa a virada de ±180°
    [InlineData(-179f, 179f)]
    public void Rotacao_toma_sempre_o_caminho_mais_curto(float de, float para)
    {
        var atual = Mathf.DegToRad(de);
        var alvo = Mathf.DegToRad(para);

        var primeiro = MovementMath.RotateToward(atual, alvo, 14f, Passo);
        var deltaPrimeiro = Mathf.Abs(Mathf.AngleDifference(atual, primeiro));
        var distanciaTotal = Mathf.Abs(Mathf.AngleDifference(atual, alvo));

        Assert.True(deltaPrimeiro <= distanciaTotal + Tol,
            "girou mais que a distância total — pegou o caminho longo");

        for (var i = 0; i < 120; i++)
            atual = MovementMath.RotateToward(atual, alvo, 14f, Passo);

        Assert.Equal(0f, Mathf.AngleDifference(atual, alvo), Tol);
    }

    [Fact]
    public void Rotacao_nao_oscila_ao_chegar_no_alvo()
    {
        var atual = 0f;
        var alvo = Mathf.DegToRad(90f);

        for (var i = 0; i < 240; i++)
            atual = MovementMath.RotateToward(atual, alvo, 14f, Passo);

        var estabilizado = atual;
        atual = MovementMath.RotateToward(atual, alvo, 14f, Passo);
        Assert.Equal(estabilizado, atual, 0.0001f);
    }

    [Fact]
    public void JumpVelocity_alcanca_exatamente_a_altura_pedida()
    {
        const float gravidade = 22f;
        const float altura = 2.2f;

        var v0 = MovementMath.JumpVelocity(gravidade, altura);

        // Mesma ordem de integração do MovementComponent.Tick de verdade:
        // a velocidade é atualizada primeiro, e SÓ DEPOIS o MoveAndSlide desloca
        // a posição usando essa velocidade já nova (Euler semi-implícito).
        // Mesmo assim, um passo fixo de 1/60 s tem um erro de discretização
        // inerente perto do pico (a velocidade cruza zero NO MEIO de um
        // quadro, não exatamente na borda) -- a tolerância generosa reflete
        // isso, não imprecisão em JumpVelocity, que é uma fórmula exata.
        var velocidadeY = v0;
        var alturaAcumulada = 0f;
        var picoAlcancado = 0f;

        while (velocidadeY > 0f)
        {
            velocidadeY = MovementMath.ApplyGravity(velocidadeY, gravidade, fallGravityScale: 1f, noChao: false, Passo);
            alturaAcumulada += velocidadeY * Passo;
            picoAlcancado = Mathf.Max(picoAlcancado, alturaAcumulada);
        }

        Assert.Equal(altura, picoAlcancado, 0.15f);
    }

    [Fact]
    public void JumpVelocity_e_zero_para_altura_zero()
    {
        Assert.Equal(0f, MovementMath.JumpVelocity(22f, 0f), Tol);
    }

    [Fact]
    public void ApplyGravity_no_chao_gruda_em_vez_de_zerar()
    {
        var v = MovementMath.ApplyGravity(0f, 22f, 1.6f, noChao: true, Passo);
        Assert.Equal(-1f, v, Tol);
    }

    [Fact]
    public void ApplyGravity_na_subida_usa_a_gravidade_base_sem_escala()
    {
        var comEscala = MovementMath.ApplyGravity(5f, 22f, fallGravityScale: 1.6f, noChao: false, Passo);
        var semEscala = MovementMath.ApplyGravity(5f, 22f, fallGravityScale: 1f, noChao: false, Passo);

        // Subindo (velocidade positiva): a escala de queda não deveria mudar nada.
        Assert.Equal(semEscala, comEscala, Tol);
    }

    [Fact]
    public void ApplyGravity_na_queda_com_escala_cai_mais_rapido_que_sem_escala()
    {
        var comEscala = MovementMath.ApplyGravity(-1f, 22f, fallGravityScale: 1.6f, noChao: false, Passo);
        var semEscala = MovementMath.ApplyGravity(-1f, 22f, fallGravityScale: 1f, noChao: false, Passo);

        Assert.True(comEscala < semEscala,
            $"a queda escalada deveria acelerar mais rápido; com escala {comEscala}, sem escala {semEscala}");
    }
}
