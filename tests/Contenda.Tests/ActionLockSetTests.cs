using Contenda.Components.Combat;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Travas de ação por fonte e com duração, somadas por OR.
/// </summary>
/// <remarks>
/// O bug clássico que isto existe para evitar: duas fontes travam o mesmo
/// movimento, uma expira, e o jogador destrava cedo demais porque alguém
/// guardou "travado: sim/não" num bool só, em vez de uma trava por fonte.
/// Ver spec 07 §8 e o ticket 08.
///
/// As fontes são <c>string</c> literais, convertidas para <c>StringName</c>
/// só dentro de cada método de teste, nunca em campo estático: construir
/// <c>StringName</c> no inicializador de tipo derruba o teste antes de rodar
/// — ver o comentário no <c>.csproj</c> deste projeto.
/// </remarks>
public sealed class ActionLockSetTests
{
    private const string Fonte1 = "fonte.um";
    private const string Fonte2 = "fonte.dois";

    [Fact]
    public void Comeca_sem_nenhuma_trava_ativa()
    {
        var travas = new ActionLockSet();
        Assert.Equal(ActionLock.None, travas.Current);
    }

    [Fact]
    public void Aplicar_uma_trava_ativa_a_flag()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 1f);

        Assert.Equal(ActionLock.Movement, travas.Current);
    }

    [Fact]
    public void Duas_fontes_diferentes_se_somam_por_OR()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 1f);
        travas.Apply(Fonte2, ActionLock.Rotation, 1f);

        Assert.Equal(ActionLock.Movement | ActionLock.Rotation, travas.Current);
    }

    [Fact]
    public void Uma_fonte_expirando_nao_libera_a_flag_que_outra_ainda_segura()
    {
        // O bug clássico: duas fontes travam Movement, uma expira, e um bool
        // único destravaria cedo demais. Aqui cada fonte tem a própria
        // contagem regressiva.
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 0.10f);
        travas.Apply(Fonte2, ActionLock.Movement, 10f);

        travas.Tick(0.20f); // expira só a Fonte1

        Assert.Equal(ActionLock.Movement, travas.Current);
    }

    [Fact]
    public void Expirar_a_ultima_fonte_libera_a_flag()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 0.10f);

        travas.Tick(0.20f);

        Assert.Equal(ActionLock.None, travas.Current);
    }

    [Fact]
    public void Tick_antes_de_expirar_nao_libera_nada()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 1f);

        travas.Tick(0.5f);

        Assert.Equal(ActionLock.Movement, travas.Current);
    }

    [Fact]
    public void Clear_libera_imediatamente_mesmo_com_duracao_restante()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 10f);

        travas.Clear(Fonte1);

        Assert.Equal(ActionLock.None, travas.Current);
    }

    [Fact]
    public void Clear_de_fonte_ausente_nao_faz_nada()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 1f);

        travas.Clear(Fonte2);

        Assert.Equal(ActionLock.Movement, travas.Current);
    }

    [Fact]
    public void Reaplicar_a_mesma_fonte_substitui_a_duracao_em_vez_de_somar()
    {
        // A fonte é quem sabe por quanto tempo ainda precisa da trava — somar
        // deixaria uma trava refrescada a cada quadro crescer sem limite.
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 0.10f);
        travas.Apply(Fonte1, ActionLock.Movement, 0.05f);

        travas.Tick(0.06f);

        Assert.Equal(ActionLock.None, travas.Current);
    }

    [Fact]
    public void Reaplicar_a_mesma_fonte_pode_trocar_as_flags()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 1f);
        travas.Apply(Fonte1, ActionLock.Rotation, 1f);

        Assert.Equal(ActionLock.Rotation, travas.Current);
    }

    [Fact]
    public void Duracao_zero_ou_negativa_equivale_a_Clear()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 1f);

        travas.Apply(Fonte1, ActionLock.Movement, 0f);

        Assert.Equal(ActionLock.None, travas.Current);
    }

    [Fact]
    public void Flags_None_equivale_a_Clear()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 1f);

        travas.Apply(Fonte1, ActionLock.None, 1f);

        Assert.Equal(ActionLock.None, travas.Current);
    }

    [Fact]
    public void Reset_limpa_todas_as_fontes()
    {
        var travas = new ActionLockSet();
        travas.Apply(Fonte1, ActionLock.Movement, 5f);
        travas.Apply(Fonte2, ActionLock.Rotation, 5f);

        travas.Reset();

        Assert.Equal(ActionLock.None, travas.Current);
    }

    [Fact]
    public void Tick_sem_nenhuma_trava_nao_da_erro()
    {
        var travas = new ActionLockSet();
        travas.Tick(1f / 60f);

        Assert.Equal(ActionLock.None, travas.Current);
    }
}
