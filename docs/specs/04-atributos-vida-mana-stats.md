# 04 — Atributos: vida, mana e stats

Um único conjunto de componentes serve jogador, inimigo comum, elite e boss.
Nada específico de Swordsman ou Gunslinger entra aqui.

## 1. `StatBlock` — a base de tudo

Transformações, buffs e equipamentos **nunca** escrevem em campos de outros
componentes. Escrevem modificadores num `StatBlock`, e os componentes **leem** o
valor final. Isso torna reverter uma transformação trivial: remove-se os
modificadores daquela fonte.

```csharp
public enum StatId : byte
{
    MaxHealth, MaxMana, ManaRegen,
    MoveSpeed, Acceleration,
    DamageMultiplier, DefenseMultiplier,
    AttackSpeed, CooldownReduction,
    KnockbackResistance
}

public enum ModifierOp : byte { Flat, PercentAdd, PercentMult }

public readonly record struct StatModifier(
    StatId Stat, ModifierOp Op, float Value, StringName Source);
```

### Ordem de aplicação

```
final = (base + Σ Flat) × (1 + Σ PercentAdd) × Π (1 + PercentMult)
```

`Flat` para itens, `PercentAdd` para buffs empilháveis, `PercentMult` para
transformações (multiplicativo, não soma com nada).

```csharp
public sealed class StatsComponent : Node, ICharacterComponent
{
    public float Get(StatId stat);                       // valor final, cacheado
    public void  AddModifier(in StatModifier m);
    public void  RemoveBySource(StringName source);      // ← reverter forma
    public event Action<StatId, float> StatChanged;      // (stat, novoValor)
}
```

O cache é invalidado por stat quando um modificador daquele stat entra ou sai —
não recalcular tudo a cada `Get` (chamado em hot path de movimento).

## 2. `HealthComponent`

```csharp
public sealed class HealthComponent : Node, ICharacterComponent, IDamageable
{
    public float MaxHealth     { get; }   // = Stats.Get(MaxHealth)
    public float CurrentHealth { get; }
    public float Percent       { get; }   // 0..1
    public bool  IsAlive       { get; }
    public bool  IsInvulnerable { get; }

    public void ApplyDamage(in DamageInfo info);   // enfileira
    public void Heal(float amount, StringName source);
    public void Kill(StringName source);
    public void ResetForSpawn();                   // ← pooling de inimigos

    public event Action<DamageInfo> Damaged;
    public event Action<float, StringName> Healed;
    public event Action<DamageInfo> Died;
}
```

### Regras

- Dano recebido no frame vai para uma fila; é resolvido num ponto único
  (ver [spec 01](01-arquitetura-tecnica.md), §6). Evita `Died` duplo.
- Mitigação: `dano_final = dano_bruto / max(0.1, Stats.Get(DefenseMultiplier))`.
  `DefenseMultiplier` 0.8 (Berserker) ⇒ recebe **25% mais** dano.
- Vida nunca ultrapassa `MaxHealth`; overheal é descartado no MVP.
- **`MaxHealth` mudou** (transformação): `CurrentHealth` é escalado
  proporcionalmente para não matar/curar de graça ao entrar e sair da forma.
- `ResetForSpawn()` zera fila, invulnerabilidade, i-frames e vida — obrigatório
  para pooling.
- Morte é idempotente: a primeira chamada dispara `Died`, as seguintes são no-op.

### Invulnerabilidade

| Fonte | Duração |
|---|---|
| i-frames pós-hit (jogador) | 0.25 s |
| i-frames pós-hit (inimigo) | 0 s |
| durante habilidade com `GrantsInvulnerability` | duração da janela |
| durante spawn de onda | 0.5 s (evita spawn-kill) |

## 3. `ManaComponent`

```csharp
public sealed class ManaComponent : Node, ICharacterComponent
{
    public float MaxMana        { get; }   // = Stats.Get(MaxMana)
    public float CurrentMana    { get; }
    public float Percent        { get; }
    public float RegenPerSecond { get; }   // = Stats.Get(ManaRegen)

    public bool CanConsume(float amount);
    public bool TryConsume(float amount, StringName source);  // atômico
    public void Drain(float amountThisFrame, StringName source); // pode zerar
    public void Restore(float amount, StringName source);
    public void ResetForSpawn();

    public event Action<float, float> ManaChanged;  // (atual, max)
    public event Action Depleted;                   // chegou a 0
}
```

### Regras

- `TryConsume` é **tudo-ou-nada**: se não há mana, retorna `false` e não altera
  nada. Habilidades usam este método.
- `Drain` é **parcial**: consome o que houver e dispara `Depleted` ao zerar.
  Transformações usam este método — o dreno por segundo não pode "falhar", ele
  esgota e força a reversão.
- Regeneração roda sempre, inclusive transformado (o dreno simplesmente supera).
- **Delay de regeneração:** 1.0 s após qualquer consumo, para que gastar mana
  tenha custo real. Configurável em `ManaDefinition`.

## 4. Resources de definição

```csharp
[GlobalClass]
public partial class HealthDefinition : Resource
{
    [Export] public float MaxHealth = 100f;
    [Export] public float InvulnerabilityAfterHit = 0.25f;
    [Export] public float KnockbackResistance = 0f;   // 0..1
}

[GlobalClass]
public partial class ManaDefinition : Resource
{
    [Export] public float MaxMana = 100f;
    [Export] public float RegenPerSecond = 5f;
    [Export] public float RegenDelayAfterSpend = 1.0f;
    [Export] public float StartingManaPercent = 1f;
}
```

## 5. Valores iniciais (MVP)

| | Swordsman | Gunslinger | Grunt | Elite | Boss |
|---|---|---|---|---|---|
| MaxHealth | 140 | 100 | 40 | 160 | 900 |
| MaxMana | 100 | 120 | — | — | 200 |
| ManaRegen /s | 5 | 7 | — | — | 10 |
| MoveSpeed m/s | 5.5 | 6.2 | 3.4 | 4.2 | 3.0 |
| Defense | 1.0 | 0.85 | 1.0 | 1.3 | 2.0 |

Tabela viva — é a fonte de verdade dos `.tres` em `data/Characters/` e
`data/Enemies/`. Toda alteração de balanceamento atualiza **aqui e no `.tres`**
no mesmo commit.

## 6. Interação com transformações

Exemplo — Berserker ativo (`Source = "form:berserker"`):

```
StatsComponent.AddModifier(DamageMultiplier,  PercentMult, +0.60, "form:berserker")
StatsComponent.AddModifier(MoveSpeed,         PercentMult, +0.15, "form:berserker")
StatsComponent.AddModifier(DefenseMultiplier, PercentMult, -0.20, "form:berserker")
```

Reversão:

```csharp
Stats.RemoveBySource("form:berserker");
```

Uma linha. É por isso que o `StatBlock` existe.

## 7. Critérios de aceite (M2)

- [ ] Levar dano reduz a barra e dispara i-frames visíveis (blink no material).
- [ ] Morte dispara `Died` **uma vez** mesmo com dois golpes no mesmo frame.
- [ ] Gastar mana pausa a regeneração por 1 s e depois ela volta.
- [ ] `TryConsume` com mana insuficiente não altera `CurrentMana`.
- [ ] Adicionar e remover um modificador devolve o stat ao valor base exato
      (comparação por epsilon em teste xUnit).
- [ ] `ResetForSpawn` devolve um inimigo reciclado ao estado de vida cheia sem
      handlers duplicados.
