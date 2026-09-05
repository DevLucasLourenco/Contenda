using Contenda.Components.Health;
using Godot;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Vida, mitigação e morte.
/// </summary>
/// <remarks>
/// O teste que mais importa é o da morte idempotente. Dois golpes no mesmo
/// quadro disparando morte duas vezes está na lista de bugs previsíveis da
/// spec 15 §5 — e no M6 significaria pontuar o mesmo abate duas vezes.
/// </remarks>
public sealed class HealthStateTests
{
    private const float Tol = 0.001f;

    private static DamageInfo Golpe(float quanto) => new(
        Amount: quanto,
        Type: DamageType.Physical,
        HitPoint: Vector3.Zero,
        Direction: Vector3.Forward,
        Knockback: 0f,
        SourceId: 1UL,
        SourceTag: "teste",
        IsCritical: false);

    [Fact]
    public void Nasce_com_a_vida_cheia()
    {
        var vida = new HealthState(100f);
        Assert.Equal(100f, vida.Current, Tol);
        Assert.True(vida.IsAlive);
        Assert.Equal(1f, vida.Percent, Tol);
    }

    [Fact]
    public void Dano_e_reduzido_pela_defesa()
    {
        var vida = new HealthState(100f);

        // Defesa 2 corta o dano pela metade; defesa 0,8 faz receber 25% a mais.
        vida.Apply(Golpe(40f), defesa: 2f);
        Assert.Equal(80f, vida.Current, Tol);

        vida.Apply(Golpe(40f), defesa: 0.8f);
        Assert.Equal(30f, vida.Current, Tol);
    }

    [Fact]
    public void Defesa_absurdamente_baixa_nao_vira_dano_infinito()
    {
        var vida = new HealthState(100f);
        vida.Apply(Golpe(1f), defesa: 0f);

        // Sem piso, dividir por zero mataria de um golpe de 1 de dano.
        Assert.True(vida.Current > 0f, "defesa zero não pode virar dano infinito");
    }

    [Fact]
    public void Dois_golpes_no_mesmo_instante_matam_UMA_vez()
    {
        var vida = new HealthState(50f);
        var mortes = 0;
        vida.Died += _ => mortes++;

        vida.Apply(Golpe(30f), 1f);
        vida.Apply(Golpe(30f), 1f);

        Assert.Equal(1, mortes);
        Assert.False(vida.IsAlive);
    }

    [Fact]
    public void Depois_de_morto_nao_toma_mais_dano_nem_avisa_de_novo()
    {
        var vida = new HealthState(10f);
        var mortes = 0;
        var golpes = 0;
        vida.Died += _ => mortes++;
        vida.Damaged += _ => golpes++;

        vida.Apply(Golpe(20f), 1f);
        vida.Apply(Golpe(20f), 1f);
        vida.Apply(Golpe(20f), 1f);

        Assert.Equal(1, mortes);
        Assert.Equal(1, golpes);
        Assert.Equal(0f, vida.Current, Tol);
    }

    [Fact]
    public void A_vida_nunca_fica_negativa()
    {
        var vida = new HealthState(10f);
        vida.Apply(Golpe(999f), 1f);
        Assert.Equal(0f, vida.Current, Tol);
    }

    [Fact]
    public void Invulneravel_ignora_o_golpe_por_completo()
    {
        var vida = new HealthState(100f) { IsInvulnerable = true };
        var golpes = 0;
        vida.Damaged += _ => golpes++;

        var aplicou = vida.Apply(Golpe(40f), 1f);

        Assert.False(aplicou);
        Assert.Equal(100f, vida.Current, Tol);
        Assert.Equal(0, golpes);
    }

    [Fact]
    public void Curar_nao_passa_do_maximo_nem_ressuscita()
    {
        var vida = new HealthState(100f);
        vida.Apply(Golpe(30f), 1f);

        vida.Heal(999f);
        Assert.Equal(100f, vida.Current, Tol);

        vida.Apply(Golpe(999f), 1f);
        vida.Heal(50f);
        Assert.Equal(0f, vida.Current, Tol);
        Assert.False(vida.IsAlive);
    }

    [Fact]
    public void Mudar_a_vida_maxima_NAO_mata_nem_cura_de_graca()
    {
        // Quando uma transformação muda a vida máxima, a atual acompanha em
        // proporção: sem isso, entrar numa forma curaria e sair mataria.
        var vida = new HealthState(100f);
        vida.Apply(Golpe(50f), 1f);
        Assert.Equal(0.5f, vida.Percent, Tol);

        vida.SetMax(200f);
        Assert.Equal(100f, vida.Current, Tol);
        Assert.Equal(0.5f, vida.Percent, Tol);

        vida.SetMax(100f);
        Assert.Equal(50f, vida.Current, Tol);
        Assert.Equal(0.5f, vida.Percent, Tol);
    }

    [Fact]
    public void Mudar_o_maximo_de_um_morto_nao_o_traz_de_volta()
    {
        var vida = new HealthState(100f);
        vida.Apply(Golpe(999f), 1f);

        vida.SetMax(200f);

        Assert.False(vida.IsAlive);
        Assert.Equal(0f, vida.Current, Tol);
    }

    [Fact]
    public void Resetar_devolve_ao_estado_de_recem_criado()
    {
        // Contrato de reciclagem do pool de inimigos, no M5. O estado sujo é
        // completo de propósito: morto E invulnerável, que é como um inimigo
        // pode chegar ao pool se morreu logo depois de apanhar.
        var vida = new HealthState(100f);
        vida.Apply(Golpe(999f), 1f);
        vida.IsInvulnerable = true;
        Assert.False(vida.IsAlive);

        vida.Reset();

        Assert.True(vida.IsAlive);
        Assert.Equal(100f, vida.Current, Tol);
        Assert.False(vida.IsInvulnerable);
    }

    [Fact]
    public void Um_alvo_resetado_pode_morrer_de_novo_e_avisar_de_novo()
    {
        var vida = new HealthState(10f);
        var mortes = 0;
        vida.Died += _ => mortes++;

        vida.Apply(Golpe(20f), 1f);
        vida.Reset();
        vida.Apply(Golpe(20f), 1f);

        Assert.Equal(2, mortes);
    }

    [Fact]
    public void O_aviso_de_dano_carrega_o_golpe_que_o_causou()
    {
        var vida = new HealthState(100f);
        DamageInfo? recebido = null;
        vida.Damaged += info => recebido = info;

        vida.Apply(Golpe(25f) with { SourceTag = "weapon.sword" }, 1f);

        Assert.NotNull(recebido);
        Assert.Equal("weapon.sword", recebido!.Value.SourceTag);
    }
}
