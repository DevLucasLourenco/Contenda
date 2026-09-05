using System;
using System.Collections.Generic;

namespace Contenda.Components.Stats;

/// <summary>Atributos numéricos de uma entidade.</summary>
public enum StatId : byte
{
    MaxHealth,
    MaxMana,
    ManaRegen,
    MoveSpeed,
    Acceleration,
    DamageMultiplier,
    DefenseMultiplier,
    AttackSpeed,
    CooldownReduction,
    KnockbackResistance,
    CritChance,
    CritMultiplier,
}

/// <summary>Como um modificador se combina com os outros.</summary>
public enum ModifierOp : byte
{
    /// <summary>Somado à base. Para itens.</summary>
    Flat,

    /// <summary>Percentuais que somam entre si. Para buffs empilháveis.</summary>
    PercentAdd,

    /// <summary>Percentuais que multiplicam entre si. Para transformações.</summary>
    PercentMult,
}

/// <summary>
/// Uma alteração de atributo, marcada com sua origem.
/// </summary>
/// <param name="Stat">Qual atributo altera.</param>
/// <param name="Op">Como se combina.</param>
/// <param name="Value">Quanto altera.</param>
/// <param name="Source">Quem o aplicou. É por aqui que se reverte.</param>
public readonly record struct StatModifier(
    StatId Stat, ModifierOp Op, float Value, string Source);

/// <summary>
/// Base mais modificadores, com o valor final derivado.
/// </summary>
/// <remarks>
/// **É o coração da reversibilidade do projeto.** Transformações, buffs e
/// equipamentos nunca escrevem em campos de outros componentes: registram
/// modificadores marcados com a origem, e os componentes leem o valor final.
/// Reverter uma transformação vira uma chamada só, em vez de desfazer alterações
/// espalhadas — que é como se perde um atributo pelo caminho. Ver ADR-006.
///
/// POCO de propósito: não herda de <c>Node</c>, então roda em xUnit sem engine.
/// </remarks>
public sealed class StatBlock
{
    private readonly Dictionary<StatId, float> _bases = [];
    private readonly List<StatModifier> _modificadores = [];
    private readonly Dictionary<StatId, float> _cache = [];

    /// <summary>Avisa quando um atributo muda de valor final.</summary>
    public event Action<StatId, float>? StatChanged;

    /// <summary>
    /// Valor neutro de um atributo que ninguém definiu.
    /// </summary>
    /// <remarks>
    /// Multiplicadores partem de 1, não de 0. Partir de zero faria todo dano
    /// virar zero até alguém lembrar de definir a base — e o sintoma seria
    /// "meus golpes não machucam", longe da causa.
    /// </remarks>
    private static float Neutro(StatId stat) => stat switch
    {
        StatId.DamageMultiplier or StatId.DefenseMultiplier
            or StatId.AttackSpeed or StatId.CritMultiplier => 1f,
        _ => 0f,
    };

    /// <summary>Define o valor base de um atributo.</summary>
    public void SetBase(StatId stat, float valor)
    {
        _bases[stat] = valor;
        Invalidar(stat);
    }

    /// <summary>Valor final, com todos os modificadores aplicados.</summary>
    public float Get(StatId stat)
    {
        if (_cache.TryGetValue(stat, out var pronto))
            return pronto;

        var valor = Calcular(stat);
        _cache[stat] = valor;
        return valor;
    }

    /// <summary>Registra um modificador.</summary>
    public void AddModifier(in StatModifier modificador)
    {
        _modificadores.Add(modificador);
        Invalidar(modificador.Stat);
    }

    /// <summary>
    /// Remove todos os modificadores de uma origem.
    /// </summary>
    /// <remarks>
    /// A reversão é por REMOÇÃO, nunca somando o inverso: somar o inverso deixa
    /// resíduo de ponto flutuante, e o resíduo se acumula a cada transformação
    /// até o atributo não voltar mais ao valor base.
    /// </remarks>
    public void RemoveBySource(string origem)
    {
        HashSet<StatId>? afetados = null;

        for (var i = _modificadores.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(_modificadores[i].Source, origem, StringComparison.Ordinal))
                continue;

            (afetados ??= []).Add(_modificadores[i].Stat);
            _modificadores.RemoveAt(i);
        }

        if (afetados is null)
            return;

        foreach (var stat in afetados)
            Invalidar(stat);
    }

    /// <summary>
    /// Remove todos os modificadores, de todas as origens.
    /// </summary>
    /// <remarks>Parte do contrato de reciclagem do pool de inimigos, no M5.</remarks>
    public void ClearModifiers()
    {
        if (_modificadores.Count == 0)
            return;

        var afetados = new HashSet<StatId>();
        foreach (var m in _modificadores)
            afetados.Add(m.Stat);

        _modificadores.Clear();

        foreach (var stat in afetados)
            Invalidar(stat);
    }

    private void Invalidar(StatId stat)
    {
        _cache.Remove(stat);
        StatChanged?.Invoke(stat, Get(stat));
    }

    private float Calcular(StatId stat)
    {
        var valor = _bases.TryGetValue(stat, out var b) ? b : Neutro(stat);

        var somaPercentual = 0f;
        var produto = 1f;

        // Ordem: (base + Σ Flat) × (1 + Σ PercentAdd) × Π (1 + PercentMult).
        // Um laço só, sem LINQ nem alocação — isto é lido em caminho de quadro.
        for (var i = 0; i < _modificadores.Count; i++)
        {
            var m = _modificadores[i];
            if (m.Stat != stat)
                continue;

            switch (m.Op)
            {
                case ModifierOp.Flat: valor += m.Value; break;
                case ModifierOp.PercentAdd: somaPercentual += m.Value; break;
                case ModifierOp.PercentMult: produto *= 1f + m.Value; break;
                default: break;
            }
        }

        return valor * (1f + somaPercentual) * produto;
    }
}
