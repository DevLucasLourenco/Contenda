using Contenda.Components.Movement;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// O avanço do dash — ticket 17, spec 16 §4.
/// </summary>
public sealed class DashStateTests
{
    private const float Tol = 0.001f;

    [Fact]
    public void Recem_criado_nao_esta_ativo()
    {
        var dash = new DashState();
        Assert.False(dash.IsActive);
        Assert.Equal(Vector3.Zero, dash.Velocity);
    }

    [Fact]
    public void Start_ativa_com_velocidade_distancia_sobre_duracao()
    {
        var dash = new DashState();
        dash.Start(new Vector3(0f, 0f, -1f), distancia: 5f, duracao: 0.18f);

        Assert.True(dash.IsActive);
        Assert.Equal(5f / 0.18f, dash.Velocity.Length(), Tol);
        Assert.Equal(-1f, dash.Velocity.Normalized().Z, Tol);
    }

    [Fact]
    public void Velocidade_fica_constante_ate_o_fim_da_duracao()
    {
        var dash = new DashState();
        dash.Start(Vector3.Forward, distancia: 5f, duracao: 0.18f);

        var velocidadeInicial = dash.Velocity;
        dash.Advance(0.1f);

        Assert.True(dash.IsActive);
        Assert.Equal(velocidadeInicial, dash.Velocity);
    }

    [Fact]
    public void Termina_sozinho_ao_esgotar_a_duracao()
    {
        var dash = new DashState();
        dash.Start(Vector3.Forward, distancia: 5f, duracao: 0.18f);

        dash.Advance(0.18f);

        Assert.False(dash.IsActive);
        Assert.Equal(Vector3.Zero, dash.Velocity);
    }

    [Fact]
    public void Termina_com_a_soma_de_muitos_quadros_pequenos_e_nao_so_com_um_grande()
    {
        var dash = new DashState();
        dash.Start(Vector3.Forward, distancia: 5f, duracao: 0.18f);

        var passo = 1f / 60f;
        var quadros = 0;
        while (dash.IsActive && quadros < 1000)
        {
            dash.Advance(passo);
            quadros++;
        }

        Assert.False(dash.IsActive);
        Assert.True(quadros is > 9 and < 13, $"deveria terminar por volta de 0,18s / (1/60s) ~= 11 quadros; terminou em {quadros}");
    }

    [Fact]
    public void Duracao_zero_ou_negativa_nao_inicia_nada()
    {
        var dash = new DashState();
        dash.Start(Vector3.Forward, distancia: 5f, duracao: 0f);

        Assert.False(dash.IsActive);
    }

    [Fact]
    public void Distancia_zero_ou_negativa_nao_inicia_nada()
    {
        var dash = new DashState();
        dash.Start(Vector3.Forward, distancia: 0f, duracao: 0.18f);

        Assert.False(dash.IsActive);
    }

    [Fact]
    public void Cancel_interrompe_um_dash_em_andamento()
    {
        var dash = new DashState();
        dash.Start(Vector3.Forward, distancia: 5f, duracao: 0.18f);

        dash.Cancel();

        Assert.False(dash.IsActive);
        Assert.Equal(Vector3.Zero, dash.Velocity);
    }

    [Fact]
    public void Um_novo_Start_substitui_o_dash_em_andamento()
    {
        var dash = new DashState();
        dash.Start(Vector3.Forward, distancia: 5f, duracao: 0.18f);
        dash.Advance(0.1f);

        dash.Start(Vector3.Right, distancia: 3f, duracao: 0.1f);

        Assert.True(dash.IsActive);
        Assert.Equal(1f, dash.Velocity.Normalized().X, Tol);
        Assert.Equal(3f / 0.1f, dash.Velocity.Length(), Tol);
    }
}
