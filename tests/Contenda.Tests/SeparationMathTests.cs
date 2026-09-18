using System.Collections.Generic;
using Contenda.Components.AI;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>Força de separação entre inimigos próximos — ticket 23, spec 09 §4.</summary>
public sealed class SeparationMathTests
{
    [Fact]
    public void Sem_vizinhos_forca_e_zero()
    {
        var forca = SeparationMath.ComputeForce(Vector3.Zero, new List<Vector3>(), raio: 1.2f, peso: 0.35f);

        Assert.Equal(Vector3.Zero, forca);
    }

    [Fact]
    public void Vizinho_fora_do_raio_nao_empurra()
    {
        var vizinhos = new List<Vector3> { new(5f, 0f, 0f) };

        var forca = SeparationMath.ComputeForce(Vector3.Zero, vizinhos, raio: 1.2f, peso: 0.35f);

        Assert.Equal(Vector3.Zero, forca);
    }

    [Fact]
    public void Vizinho_dentro_do_raio_empurra_para_longe()
    {
        var vizinhos = new List<Vector3> { new(1f, 0f, 0f) }; // a leste, dentro de 1.2 m

        var forca = SeparationMath.ComputeForce(Vector3.Zero, vizinhos, raio: 1.2f, peso: 0.35f);

        Assert.True(forca.X < 0f, "deveria empurrar para OESTE, longe do vizinho a leste");
        Assert.Equal(0f, forca.Z, precision: 5);
    }

    [Fact]
    public void Vizinho_mais_perto_empurra_mais_forte()
    {
        var perto = SeparationMath.ComputeForce(Vector3.Zero, new List<Vector3> { new(0.3f, 0f, 0f) }, raio: 1.2f, peso: 0.35f);
        var longe = SeparationMath.ComputeForce(Vector3.Zero, new List<Vector3> { new(1.0f, 0f, 0f) }, raio: 1.2f, peso: 0.35f);

        Assert.True(perto.Length() > longe.Length());
    }

    [Fact]
    public void Achatada_no_plano_ignora_diferenca_de_altura()
    {
        var vizinhos = new List<Vector3> { new(0f, 5f, 1f) }; // 5 m acima, 1 m ao norte

        var forca = SeparationMath.ComputeForce(Vector3.Zero, vizinhos, raio: 1.2f, peso: 0.35f);

        Assert.Equal(0f, forca.Y);
    }

    [Fact]
    public void Vizinho_na_mesma_posicao_nao_produz_forca_nem_divisao_por_zero()
    {
        var vizinhos = new List<Vector3> { Vector3.Zero };

        var forca = SeparationMath.ComputeForce(Vector3.Zero, vizinhos, raio: 1.2f, peso: 0.35f);

        Assert.Equal(Vector3.Zero, forca);
    }

    [Fact]
    public void Varios_vizinhos_somam_as_forcas()
    {
        var vizinhos = new List<Vector3> { new(1f, 0f, 0f), new(-1f, 0f, 0f) };

        var forca = SeparationMath.ComputeForce(Vector3.Zero, vizinhos, raio: 1.2f, peso: 0.35f);

        // Empurrões simétricos e opostos deveriam se cancelar no X.
        Assert.Equal(0f, forca.X, precision: 4);
    }

    [Fact]
    public void Peso_zero_neutraliza_a_forca()
    {
        var vizinhos = new List<Vector3> { new(0.5f, 0f, 0f) };

        var forca = SeparationMath.ComputeForce(Vector3.Zero, vizinhos, raio: 1.2f, peso: 0f);

        Assert.Equal(Vector3.Zero, forca);
    }
}
