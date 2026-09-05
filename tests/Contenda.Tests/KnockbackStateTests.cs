using Contenda.Components.Movement;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Repulsão que decai numa duração fixa — ticket 11.
/// </summary>
/// <remarks>
/// A implementação anterior decaía a uma taxa CONSTANTE (m/s²), então o tempo
/// até zerar variava com o tamanho do impulso — um golpe forte demorava mais
/// para parar de empurrar. A spec pede o oposto: decair em 0,25 s **sempre**,
/// não importa o tamanho do impulso. A velocidade de decaimento é fixada uma
/// vez a cada <see cref="KnockbackState.Apply"/>, como o <c>LungeMotion</c> já
/// faz para o avanço do golpe — recalculá-la a cada quadro a partir do que
/// resta produz uma curva exponencial que nunca chega a zero de verdade
/// (mesmo bug que o <c>DamageLayerState</c> evitou no ticket 12).
/// </remarks>
public sealed class KnockbackStateTests
{
    private const float Tol = 0.001f;
    private const float Duration = 0.25f;

    private static void AssertVector3(Vector3 esperado, Vector3 real, float tolerancia = Tol)
    {
        Assert.Equal(esperado.X, real.X, tolerancia);
        Assert.Equal(esperado.Y, real.Y, tolerancia);
        Assert.Equal(esperado.Z, real.Z, tolerancia);
    }

    [Fact]
    public void Comeca_sem_repulsao()
    {
        var estado = new KnockbackState(Duration);
        Assert.Equal(Vector3.Zero, estado.Current);
    }

    [Fact]
    public void Aplicar_um_impulso_vira_a_repulsao_atual()
    {
        var estado = new KnockbackState(Duration);
        estado.Apply(new Vector3(4f, 0f, 0f));

        Assert.Equal(new Vector3(4f, 0f, 0f), estado.Current);
    }

    [Fact]
    public void Decai_ate_zero_exatamente_na_duracao_configurada()
    {
        var estado = new KnockbackState(Duration);
        estado.Apply(new Vector3(4f, 0f, 0f));

        estado.Advance(Duration);

        Assert.Equal(Vector3.Zero, estado.Current);
    }

    [Fact]
    public void Nao_decai_antes_da_hora()
    {
        var estado = new KnockbackState(Duration);
        estado.Apply(new Vector3(4f, 0f, 0f));

        estado.Advance(Duration * 0.5f);

        AssertVector3(new Vector3(2f, 0f, 0f), estado.Current);
    }

    [Fact]
    public void Decai_linearmente_sob_muitos_quadros_pequenos()
    {
        // O mesmo cuidado do DamageLayerState: um Tick só não prova nada sobre
        // recalcular a taxa a cada quadro. Muitos quadros pequenos provam.
        var estado = new KnockbackState(Duration);
        estado.Apply(new Vector3(9f, 0f, 0f));

        const float passo = 1f / 60f;
        for (var t = 0f; t < Duration; t += passo)
            estado.Advance(passo);

        AssertVector3(Vector3.Zero, estado.Current);
    }

    [Fact]
    public void Um_impulso_maior_leva_o_MESMO_tempo_para_decair()
    {
        // O ponto central da mudança: dobrar o impulso não pode dobrar o
        // tempo até parar.
        var pequeno = new KnockbackState(Duration);
        pequeno.Apply(new Vector3(1f, 0f, 0f));

        var grande = new KnockbackState(Duration);
        grande.Apply(new Vector3(50f, 0f, 0f));

        pequeno.Advance(Duration);
        grande.Advance(Duration);

        AssertVector3(Vector3.Zero, pequeno.Current);
        AssertVector3(Vector3.Zero, grande.Current);
    }

    [Fact]
    public void Nunca_ultrapassa_zero_e_inverte_a_direcao()
    {
        var estado = new KnockbackState(Duration);
        estado.Apply(new Vector3(4f, 0f, 0f));

        estado.Advance(Duration * 10f); // bem além do necessário

        Assert.Equal(Vector3.Zero, estado.Current);
    }

    [Fact]
    public void Um_segundo_impulso_soma_ao_que_ja_existe_e_reinicia_a_janela()
    {
        var estado = new KnockbackState(Duration);
        estado.Apply(new Vector3(4f, 0f, 0f));
        estado.Advance(Duration * 0.5f); // resta (2,0)

        estado.Apply(new Vector3(0f, 0f, -2f)); // novo golpe, direção diferente

        AssertVector3(new Vector3(2f, 0f, -2f), estado.Current);

        // A nova janela cobre a DURATION inteira a partir de agora, pelo total
        // combinado -- não pelos 0,125s que sobravam do primeiro golpe.
        estado.Advance(Duration);
        AssertVector3(Vector3.Zero, estado.Current);
    }

    [Fact]
    public void Duracao_zero_zera_no_proximo_Advance()
    {
        var estado = new KnockbackState(0f);
        estado.Apply(new Vector3(4f, 0f, 0f));

        estado.Advance(1f / 60f);

        Assert.Equal(Vector3.Zero, estado.Current);
    }
}
