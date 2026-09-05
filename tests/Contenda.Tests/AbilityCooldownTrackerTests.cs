using Contenda.Components.Abilities;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Recarga por habilidade — ticket 14.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>ActionLockSet</c>. Guarda "pronta ÀS" em vez
/// de contar delta pra baixo: consultar não precisa de um <c>Tick</c> por
/// quadro, só do relógio de quem chama — o mesmo motivo por trás do
/// <c>HitstopState</c> não se importar com quando foi a última chamada.
///
/// Fonte é <c>string</c>, não <c>StringName</c>: construir <c>StringName</c>
/// fora do processo do Godot derruba o processo. Mesma escolha do
/// <c>ActionLockSet</c> e do <c>DamageInfo.SourceTag</c>.
/// </remarks>
public sealed class AbilityCooldownTrackerTests
{
    private const float Tol = 0.001f;

    [Fact]
    public void Uma_habilidade_nunca_usada_esta_pronta()
    {
        var recargas = new AbilityCooldownTracker();
        Assert.True(recargas.IsReady("dash_slash", now: 0f));
        Assert.Equal(0f, recargas.RemainingAt("dash_slash", now: 0f), Tol);
    }

    [Fact]
    public void Iniciar_a_recarga_deixa_a_habilidade_indisponivel()
    {
        var recargas = new AbilityCooldownTracker();
        recargas.Start("dash_slash", duration: 3f, now: 10f);

        Assert.False(recargas.IsReady("dash_slash", now: 10f));
        Assert.Equal(3f, recargas.RemainingAt("dash_slash", now: 10f), Tol);
    }

    [Fact]
    public void Fica_pronta_de_novo_exatamente_quando_a_recarga_termina()
    {
        var recargas = new AbilityCooldownTracker();
        recargas.Start("dash_slash", duration: 3f, now: 10f);

        Assert.False(recargas.IsReady("dash_slash", now: 12.99f));
        Assert.True(recargas.IsReady("dash_slash", now: 13f));
        Assert.Equal(0f, recargas.RemainingAt("dash_slash", now: 13f), Tol);
    }

    [Fact]
    public void RemainingAt_nunca_fica_negativo_muito_depois_da_recarga()
    {
        var recargas = new AbilityCooldownTracker();
        recargas.Start("dash_slash", duration: 3f, now: 0f);

        Assert.Equal(0f, recargas.RemainingAt("dash_slash", now: 100f), Tol);
    }

    [Fact]
    public void Habilidades_diferentes_recarregam_independente()
    {
        var recargas = new AbilityCooldownTracker();
        recargas.Start("dash_slash", duration: 3f, now: 0f);

        Assert.True(recargas.IsReady("rising_slash", now: 0f));
    }

    [Fact]
    public void Um_novo_Start_substitui_a_recarga_em_andamento_em_vez_de_somar()
    {
        var recargas = new AbilityCooldownTracker();
        recargas.Start("dash_slash", duration: 3f, now: 0f);
        recargas.Start("dash_slash", duration: 5f, now: 1f); // reexecutar reinicia, não soma

        Assert.Equal(5f, recargas.RemainingAt("dash_slash", now: 1f), Tol);
    }

    [Fact]
    public void Duracao_zero_ou_negativa_fica_pronta_na_hora()
    {
        var recargas = new AbilityCooldownTracker();
        recargas.Start("dash_slash", duration: 0f, now: 0f);

        Assert.True(recargas.IsReady("dash_slash", now: 0f));
    }

    [Fact]
    public void Reset_limpa_todas_as_recargas()
    {
        var recargas = new AbilityCooldownTracker();
        recargas.Start("dash_slash", duration: 3f, now: 0f);
        recargas.Start("rising_slash", duration: 5f, now: 0f);

        recargas.Reset();

        Assert.True(recargas.IsReady("dash_slash", now: 0f));
        Assert.True(recargas.IsReady("rising_slash", now: 0f));
    }
}
