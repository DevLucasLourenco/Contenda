using Contenda.Weapons;
using Xunit;

namespace Contenda.Tests;

/// <summary>Gatilho da estocada de queda — ticket 19, spec 16 §6.</summary>
public sealed class AerialCombatMathTests
{
    [Fact]
    public void Comeca_no_ar_caindo_com_botao_segurado_e_sem_ataque()
    {
        Assert.True(AerialCombatMath.DeveComecarMergulho(
            noAr: true, velocidadeY: -1f, botaoSegurado: true, emAtaque: false, jaMergulhando: false));
    }

    [Fact]
    public void No_pico_exato_ja_conta_como_depois_do_pico()
    {
        Assert.True(AerialCombatMath.DeveComecarMergulho(
            noAr: true, velocidadeY: 0f, botaoSegurado: true, emAtaque: false, jaMergulhando: false));
    }

    [Fact]
    public void Nao_comeca_ainda_subindo()
    {
        Assert.False(AerialCombatMath.DeveComecarMergulho(
            noAr: true, velocidadeY: 5f, botaoSegurado: true, emAtaque: false, jaMergulhando: false));
    }

    [Fact]
    public void Nao_comeca_no_chao()
    {
        Assert.False(AerialCombatMath.DeveComecarMergulho(
            noAr: false, velocidadeY: -1f, botaoSegurado: true, emAtaque: false, jaMergulhando: false));
    }

    [Fact]
    public void Nao_comeca_sem_segurar_o_botao()
    {
        Assert.False(AerialCombatMath.DeveComecarMergulho(
            noAr: true, velocidadeY: -1f, botaoSegurado: false, emAtaque: false, jaMergulhando: false));
    }

    [Fact]
    public void Nao_comeca_com_um_golpe_em_andamento()
    {
        Assert.False(AerialCombatMath.DeveComecarMergulho(
            noAr: true, velocidadeY: -1f, botaoSegurado: true, emAtaque: true, jaMergulhando: false));
    }

    [Fact]
    public void Nao_comeca_de_novo_se_ja_esta_mergulhando()
    {
        Assert.False(AerialCombatMath.DeveComecarMergulho(
            noAr: true, velocidadeY: -1f, botaoSegurado: true, emAtaque: false, jaMergulhando: true));
    }
}
