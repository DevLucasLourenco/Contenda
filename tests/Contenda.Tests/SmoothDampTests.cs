using Contenda.Camera;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// A suavização do acompanhamento da câmera.
/// </summary>
/// <remarks>
/// O teste que importa aqui é o de independência de framerate. Usar
/// <c>Lerp(atual, alvo, delta)</c> "funciona" na máquina de quem escreveu e se
/// comporta diferente em outra — é o tipo de defeito que só aparece como
/// reclamação vaga de que "a câmera parece diferente no meu PC".
/// </remarks>
public sealed class SmoothDampTests
{
    private static float SimularAte(float segundos, float passo, float alvo)
    {
        float atual = 0f, velocidade = 0f;
        for (float t = 0f; t < segundos; t += passo)
            atual = CameraMath.SmoothDamp(atual, alvo, ref velocidade, 0.16f, passo);
        return atual;
    }

    [Fact]
    public void O_resultado_nao_depende_do_framerate()
    {
        var a60 = SimularAte(1f, 1f / 60f, 10f);
        var a144 = SimularAte(1f, 1f / 144f, 10f);
        var a30 = SimularAte(1f, 1f / 30f, 10f);

        // Um segundo de perseguição com tempo de 0,16 s chega essencialmente ao
        // alvo em qualquer taxa. A tolerância cobre a discretização do passo.
        Assert.Equal(a60, a144, 0.05f);
        Assert.Equal(a60, a30, 0.05f);
    }

    [Fact]
    public void Converge_para_o_alvo_sem_ultrapassar()
    {
        float atual = 0f, velocidade = 0f;
        for (var i = 0; i < 240; i++)
        {
            atual = CameraMath.SmoothDamp(atual, 10f, ref velocidade, 0.16f, 1f / 60f);
            Assert.True(atual <= 10.0001f, $"ultrapassou o alvo no passo {i}: {atual}");
        }

        Assert.Equal(10f, atual, 0.01f);
    }

    [Fact]
    public void Tempo_menor_chega_mais_rapido()
    {
        float rapido = 0f, vr = 0f, lento = 0f, vl = 0f;
        for (var i = 0; i < 10; i++)
        {
            rapido = CameraMath.SmoothDamp(rapido, 10f, ref vr, 0.08f, 1f / 60f);
            lento = CameraMath.SmoothDamp(lento, 10f, ref vl, 0.55f, 1f / 60f);
        }

        Assert.True(rapido > lento, "tempo menor deveria estar mais perto do alvo");
    }

    [Fact]
    public void Delta_zero_nao_move_nem_divide_por_zero()
    {
        float atual = 3f, velocidade = 0f;
        Assert.Equal(3f, CameraMath.SmoothDamp(atual, 10f, ref velocidade, 0.16f, 0f));
    }

    [Fact]
    public void Tempo_zero_vai_direto_ao_alvo()
    {
        float velocidade = 5f;
        Assert.Equal(10f, CameraMath.SmoothDamp(0f, 10f, ref velocidade, 0f, 1f / 60f));
        Assert.Equal(0f, velocidade);
    }

    [Fact]
    public void A_altura_acompanha_mais_devagar_que_o_plano()
    {
        // É o que impede a câmera de saltar junto com o pulo do jogador.
        var atual = Vector3.Zero;
        var velocidade = Vector3.Zero;
        var alvo = new Vector3(10f, 10f, 10f);

        for (var i = 0; i < 10; i++)
            atual = CameraMath.SmoothDamp(atual, alvo, ref velocidade, 0.16f, 0.55f, 1f / 60f);

        Assert.True(atual.Y < atual.X, "a altura deveria ficar para trás do plano");
        Assert.Equal(atual.X, atual.Z, 0.0001f);
    }
}
