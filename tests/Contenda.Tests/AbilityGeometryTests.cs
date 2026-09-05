using Contenda.Components.Abilities;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Geometria pura compartilhada pelos comportamentos de habilidade — ticket 15.
/// </summary>
public sealed class AbilityGeometryTests
{
    private const float Tol = 0.0001f;

    [Fact]
    public void FlattenedForward_da_identidade_aponta_para_menos_Z()
    {
        var frente = AbilityGeometry.FlattenedForward(Basis.Identity);

        Assert.Equal(0f, frente.X, Tol);
        Assert.Equal(0f, frente.Y, Tol);
        Assert.Equal(-1f, frente.Z, Tol);
    }

    [Fact]
    public void FlattenedForward_giro_de_90_graus_aponta_para_menos_X()
    {
        var giro = new Basis(Vector3.Up, Mathf.DegToRad(90f));
        var frente = AbilityGeometry.FlattenedForward(giro);

        Assert.Equal(-1f, frente.X, Tol);
        Assert.Equal(0f, frente.Z, Tol);
    }

    [Fact]
    public void FlattenedForward_sempre_devolve_vetor_unitario()
    {
        var giro = new Basis(Vector3.Up, Mathf.DegToRad(37f));
        var frente = AbilityGeometry.FlattenedForward(giro);

        Assert.Equal(1f, frente.Length(), Tol);
    }

    [Fact]
    public void AimOrFlattenedForward_usa_a_mira_quando_ela_e_valida()
    {
        var mira = new Vector3(1f, 0f, 0f);
        var direcao = AbilityGeometry.AimOrFlattenedForward(mira, Basis.Identity);

        Assert.Equal(1f, direcao.X, Tol);
        Assert.Equal(0f, direcao.Z, Tol);
    }

    [Fact]
    public void AimOrFlattenedForward_cai_para_a_frente_do_corpo_sem_mira()
    {
        var direcao = AbilityGeometry.AimOrFlattenedForward(Vector3.Zero, Basis.Identity);

        Assert.Equal(0f, direcao.X, Tol);
        Assert.Equal(-1f, direcao.Z, Tol);
    }

    [Fact]
    public void AimOrFlattenedForward_achata_a_mira_com_componente_vertical()
    {
        var mira = new Vector3(0f, 5f, -1f);
        var direcao = AbilityGeometry.AimOrFlattenedForward(mira, Basis.Identity);

        Assert.Equal(0f, direcao.Y, Tol);
        Assert.Equal(1f, direcao.Length(), Tol);
    }
}
