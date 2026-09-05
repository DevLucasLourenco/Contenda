using Contenda.Components.Stats;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// O bloco de atributos com modificadores por fonte.
/// </summary>
/// <remarks>
/// O teste que carrega o resto do jogo é o de reversão exata: transformações,
/// buffs e equipamentos escrevem modificadores marcados com sua origem, e
/// reverter é remover por origem. Se sobrar resíduo aqui, o M4 vira uma fonte
/// permanente de bugs — ver ADR-006.
/// </remarks>
public sealed class StatBlockTests
{
    private const float Tol = 0.0001f;

    private static StatBlock ComBase(StatId id, float valor)
    {
        var bloco = new StatBlock();
        bloco.SetBase(id, valor);
        return bloco;
    }

    [Fact]
    public void Sem_modificador_o_valor_e_a_base()
    {
        var bloco = ComBase(StatId.MoveSpeed, 5.5f);
        Assert.Equal(5.5f, bloco.Get(StatId.MoveSpeed), Tol);
    }

    [Fact]
    public void Stat_nunca_definido_devolve_o_padrao_do_proprio_stat()
    {
        // Multiplicadores partem de 1, não de 0: partir de zero faria todo dano
        // virar zero até alguém lembrar de definir a base.
        var bloco = new StatBlock();
        Assert.Equal(1f, bloco.Get(StatId.DamageMultiplier), Tol);
        Assert.Equal(1f, bloco.Get(StatId.DefenseMultiplier), Tol);
        Assert.Equal(0f, bloco.Get(StatId.MoveSpeed), Tol);
    }

    [Fact]
    public void Adicionar_e_remover_por_fonte_devolve_ao_valor_base_EXATO()
    {
        var bloco = ComBase(StatId.DamageMultiplier, 1f);

        bloco.AddModifier(new StatModifier(
            StatId.DamageMultiplier, ModifierOp.PercentMult, 0.6f, "form:berserker"));
        Assert.Equal(1.6f, bloco.Get(StatId.DamageMultiplier), Tol);

        bloco.RemoveBySource("form:berserker");

        // Igualdade exata, não aproximada: reverter somando o inverso deixaria
        // resíduo, e o resíduo se acumula a cada transformação.
        Assert.Equal(1f, bloco.Get(StatId.DamageMultiplier));
    }

    [Fact]
    public void Remover_uma_fonte_nao_afeta_as_outras()
    {
        var bloco = ComBase(StatId.MoveSpeed, 5f);
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.Flat, 1f, "item:botas"));
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.Flat, 2f, "form:x"));

        bloco.RemoveBySource("form:x");

        Assert.Equal(6f, bloco.Get(StatId.MoveSpeed), Tol);
    }

    [Fact]
    public void A_ordem_e_flat_depois_percentadd_depois_percentmult()
    {
        var bloco = ComBase(StatId.MoveSpeed, 10f);
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.Flat, 2f, "a"));
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.PercentAdd, 0.5f, "b"));
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.PercentMult, 1f, "c"));

        // (10 + 2) × (1 + 0,5) × (1 + 1) = 36
        Assert.Equal(36f, bloco.Get(StatId.MoveSpeed), Tol);
    }

    [Fact]
    public void PercentAdd_soma_entre_si_e_PercentMult_multiplica()
    {
        var somaveis = ComBase(StatId.MoveSpeed, 10f);
        somaveis.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.PercentAdd, 0.5f, "a"));
        somaveis.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.PercentAdd, 0.5f, "b"));
        Assert.Equal(20f, somaveis.Get(StatId.MoveSpeed), Tol);   // ×(1 + 0,5 + 0,5)

        var multiplicativos = ComBase(StatId.MoveSpeed, 10f);
        multiplicativos.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.PercentMult, 0.5f, "a"));
        multiplicativos.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.PercentMult, 0.5f, "b"));
        Assert.Equal(22.5f, multiplicativos.Get(StatId.MoveSpeed), Tol);  // ×1,5×1,5
    }

    [Fact]
    public void Modificador_de_um_stat_nao_vaza_para_outro()
    {
        var bloco = ComBase(StatId.MoveSpeed, 5f);
        bloco.SetBase(StatId.MaxHealth, 100f);
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.Flat, 10f, "a"));

        Assert.Equal(100f, bloco.Get(StatId.MaxHealth), Tol);
    }

    [Fact]
    public void Mudar_a_base_reflete_com_os_modificadores_ativos()
    {
        var bloco = ComBase(StatId.MoveSpeed, 10f);
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.PercentMult, 1f, "a"));
        Assert.Equal(20f, bloco.Get(StatId.MoveSpeed), Tol);

        bloco.SetBase(StatId.MoveSpeed, 5f);
        Assert.Equal(10f, bloco.Get(StatId.MoveSpeed), Tol);
    }

    [Fact]
    public void Avisa_quando_um_stat_muda_de_valor()
    {
        var bloco = ComBase(StatId.MoveSpeed, 5f);
        var avisos = 0;
        StatId ultimo = StatId.MaxHealth;

        bloco.StatChanged += (id, _) => { avisos++; ultimo = id; };
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.Flat, 1f, "a"));

        Assert.Equal(1, avisos);
        Assert.Equal(StatId.MoveSpeed, ultimo);
    }

    [Fact]
    public void Remover_uma_fonte_inexistente_nao_avisa_nem_altera_nada()
    {
        var bloco = ComBase(StatId.MoveSpeed, 5f);
        var avisos = 0;
        bloco.StatChanged += (_, _) => avisos++;

        bloco.RemoveBySource("nunca_existiu");

        Assert.Equal(0, avisos);
        Assert.Equal(5f, bloco.Get(StatId.MoveSpeed), Tol);
    }

    [Fact]
    public void Limpar_devolve_tudo_ao_estado_de_recem_criado()
    {
        // Contrato de reciclagem: o pool de inimigos do M5 depende disto.
        var bloco = ComBase(StatId.MoveSpeed, 5f);
        bloco.AddModifier(new StatModifier(StatId.MoveSpeed, ModifierOp.Flat, 99f, "a"));

        bloco.ClearModifiers();

        Assert.Equal(5f, bloco.Get(StatId.MoveSpeed));
    }
}
