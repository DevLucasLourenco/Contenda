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
}
