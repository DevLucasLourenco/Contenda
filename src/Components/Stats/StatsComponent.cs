using System;
using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.Stats;

/// <summary>
/// Casca de nó sobre o <see cref="StatBlock"/>.
/// </summary>
/// <remarks>
/// Não contém lógica: tudo que decide valor mora no bloco, que é POCO e
/// testável. Aqui só ficam o `[Export]` para o editor e o repasse do evento.
/// </remarks>
public sealed partial class StatsComponent : Node, ICharacterComponent
{
    /// <summary>Multiplicador de defesa base. Maior que 1 recebe menos dano.</summary>
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float BaseDefense { get; set; } = 1f;

    /// <summary>Multiplicador de dano base.</summary>
    [Export(PropertyHint.Range, "0.1,4,0.05")] public float BaseDamage { get; set; } = 1f;

    private readonly StatBlock _bloco = new();

    // Guardados aqui, não relidos da definição a cada ResetForSpawn: o pool
    // (M5) reciclariam sem reconfigurar antes -- mesma disciplina do
    // `_invulnerabilidade` em HealthComponent.
    private float _baseCritChance;
    private float _baseCritMultiplier = 1f;

    /// <summary>
    /// Avisa quando um atributo muda de valor final.
    /// </summary>
    /// <remarks>
    /// Repassado direto do bloco, sem assinatura intermediária: assinar no
    /// `Bind` criaria uma inscrição a cada re-vínculo, e nó reciclado pelo pool
    /// re-vincula. Sem estado, não há o que duplicar nem o que desassinar.
    /// </remarks>
    public event Action<StatId, float>? StatChanged
    {
        add => _bloco.StatChanged += value;
        remove => _bloco.StatChanged -= value;
    }

    public void Bind(CharacterContext contexto)
    {
    }

    public void Configure(CharacterDefinition definicao)
    {
        _bloco.SetBase(StatId.DefenseMultiplier, BaseDefense);
        _bloco.SetBase(StatId.DamageMultiplier, BaseDamage);
        _bloco.SetBase(StatId.MoveSpeed, 1f);

        // Nulo (inimigos, por enquanto) vira 0 % de chance -- spec 16 §5:
        // "Inimigos: 0.00". Nenhum `if` sobre quem é o personagem, só a
        // ausência do recurso na definição.
        _baseCritChance = definicao.Stats?.CritChance ?? 0f;
        _baseCritMultiplier = definicao.Stats?.CritMultiplier ?? 1f;
        _bloco.SetBase(StatId.CritChance, _baseCritChance);
        _bloco.SetBase(StatId.CritMultiplier, _baseCritMultiplier);
    }

    /// <summary>Valor final de um atributo.</summary>
    public float Get(StatId stat) => _bloco.Get(stat);

    /// <summary>Define o valor base de um atributo.</summary>
    public void SetBase(StatId stat, float valor) => _bloco.SetBase(stat, valor);

    /// <summary>Registra um modificador.</summary>
    public void AddModifier(in StatModifier modificador) => _bloco.AddModifier(modificador);

    /// <summary>Remove todos os modificadores de uma origem. É assim que se reverte.</summary>
    public void RemoveBySource(string origem) => _bloco.RemoveBySource(origem);

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        _bloco.ClearModifiers();
        _bloco.SetBase(StatId.DefenseMultiplier, BaseDefense);
        _bloco.SetBase(StatId.DamageMultiplier, BaseDamage);
        _bloco.SetBase(StatId.MoveSpeed, 1f);
        _bloco.SetBase(StatId.CritChance, _baseCritChance);
        _bloco.SetBase(StatId.CritMultiplier, _baseCritMultiplier);
    }

}
