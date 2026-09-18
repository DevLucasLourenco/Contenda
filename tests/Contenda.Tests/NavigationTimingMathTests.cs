using Contenda.Components.AI;
using Xunit;

namespace Contenda.Tests;

/// <summary>Quando recalcular rota, escalonado — ticket 23, spec 09 §4.</summary>
public sealed class NavigationTimingMathTests
{
    [Fact]
    public void Repath_antes_do_intervalo_e_sem_o_alvo_ter_andado_nao_repatha()
    {
        Assert.False(NavigationTimingMath.ShouldRepath(
            segundosDesdeOUltimoRepath: 0.1f, distanciaDesdeOUltimoAlvo: 0.2f, intervalo: 0.25f, distanciaMinima: 1.5f));
    }

    [Fact]
    public void Intervalo_vencido_repatha_mesmo_com_alvo_parado()
    {
        Assert.True(NavigationTimingMath.ShouldRepath(
            segundosDesdeOUltimoRepath: 0.25f, distanciaDesdeOUltimoAlvo: 0f, intervalo: 0.25f, distanciaMinima: 1.5f));
    }

    [Fact]
    public void Alvo_andou_o_bastante_repatha_mesmo_dentro_do_intervalo()
    {
        Assert.True(NavigationTimingMath.ShouldRepath(
            segundosDesdeOUltimoRepath: 0.05f, distanciaDesdeOUltimoAlvo: 1.5f, intervalo: 0.25f, distanciaMinima: 1.5f));
    }

    [Fact]
    public void Nenhum_dos_dois_gatilhos_nao_repatha()
    {
        Assert.False(NavigationTimingMath.ShouldRepath(
            segundosDesdeOUltimoRepath: 0.24f, distanciaDesdeOUltimoAlvo: 1.49f, intervalo: 0.25f, distanciaMinima: 1.5f));
    }
}
