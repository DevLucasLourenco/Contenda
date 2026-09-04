# 06 — Transformações

Regra de ouro do jogo: **toda transformação consome mana** — um custo de entrada
e um dreno contínuo. Mana zerada ⇒ reversão automática.

## 1. Modelo de controle

A especificação original dizia "scroll para seleção de transformação". Isso é
ambíguo (selecionar e ativar seriam a mesma coisa, e o jogador ativaria formas
sem querer ao rolar). A resolução adotada:

| Entrada | Efeito |
|---|---|
| **Scroll ↑** | seleciona a forma **anterior** na lista |
| **Scroll ↓** | seleciona a **próxima** forma |
| **M3** (clique da roda) | **ativa** a forma selecionada / **reverte** se já ativa |

Selecionar é gratuito e instantâneo. Ativar custa mana.

A lista sempre começa em `Normal` (índice 0), que não é uma forma — é o estado
base. Rolar com apenas uma forma disponível alterna `Normal ↔ Berserker`, o que
já dá leitura ao jogador de que existirão mais.

## 2. `TransformationDefinition`

```csharp
[GlobalClass]
public partial class TransformationDefinition : Resource
{
    [Export] public StringName Id;             // "berserker"
    [Export] public string DisplayName;        // "Berserker"
    [Export] public Texture2D Icon;
    [Export] public Color ThemeColor = Colors.OrangeRed;

    // Custo
    [Export] public float ManaActivationCost = 20f;
    [Export] public float ManaDrainPerSecond = 4f;
    [Export] public float MinimumDuration    = 1.0f;  // anti-spam de toggle

    // Modificadores (aplicados como PercentMult, Source = "form:<Id>")
    [Export] public float DamageMultiplier  = 1.6f;
    [Export] public float SpeedMultiplier   = 1.15f;
    [Export] public float DefenseMultiplier = 0.8f;
    [Export] public float AttackSpeedMultiplier = 1.0f;
    [Export] public float CooldownReduction = 0f;     // 0..0.5

    // Apresentação
    [Export] public Mesh MeshOverride;                 // null = mantém o mesh
    [Export] public Material MaterialOverlay;          // aura, emissivo
    [Export] public PackedScene ActivationVfx;
    [Export] public PackedScene PersistentVfx;
    [Export] public AnimationSet AnimationSetOverride;
    [Export] public AudioStream ActivationSfx;

    // Conteúdo adicional
    [Export] public AbilityDefinition[] UnlockedAbilities;  // somam ao resolver
    [Export] public StringName[] GrantedTags;               // "form.berserker"
}
```

## 3. `TransformationComponent`

```csharp
public sealed class TransformationComponent : Node, ICharacterComponent
{
    public IReadOnlyList<TransformationDefinition> Available { get; }
    public int SelectedIndex { get; }                  // 0 = Normal
    public TransformationDefinition Active { get; }    // null quando Normal
    public float ActiveDuration { get; }

    public void SelectNext();
    public void SelectPrevious();
    public bool TryActivateSelected();                 // M3
    public void Revert(RevertReason reason);

    public event Action<int> SelectionChanged;
    public event Action<TransformationDefinition> Activated;
    public event Action<TransformationDefinition, RevertReason> Reverted;
}

public enum RevertReason { Manual, ManaDepleted, Death, ModeReset }
```

### Ativação

```
1. SelectedIndex == 0 (Normal)?           -> se há forma ativa, Revert(Manual)
2. já existe forma ativa?                 -> Revert(Manual) e retorna
3. Mana.CanConsume(ManaActivationCost)?   -> não: feedback "sem mana", retorna
4. Mana.TryConsume(ManaActivationCost)
5. Stats.AddModifier(...) × N, Source = "form:<Id>"
6. aplica MeshOverride / MaterialOverlay / PersistentVfx / AnimationSet
7. registra GrantedTags no CharacterController
8. AbilityComponent.AddAbilities(UnlockedAbilities) -> resolver reconstruído
9. dispara Activated
```

### Dreno por frame (`_PhysicsProcess`)

```csharp
if (Active is null) return;
Mana.Drain(Active.ManaDrainPerSecond * delta, formSource);
if (Mana.CurrentMana <= 0f) Revert(RevertReason.ManaDepleted);
```

Progressão observável com 4 mana/s a partir de 100:

```
100 → 96 → 92 → 88 → ... → 4 → 0  ⇒  Revert()
```

≈ 25 s de forma sem gastar mana em nada mais. Habilidades usadas durante a forma
encurtam a duração — é o trade-off central.

### Reversão

```
1. Stats.RemoveBySource("form:<Id>")     ← devolve todos os stats de uma vez
2. restaura mesh/material/animset originais
3. remove PersistentVfx
4. remove GrantedTags
5. AbilityComponent.RemoveAbilities(UnlockedAbilities)
   — se uma habilidade desbloqueada estava em execução, ela é cancelada
6. dispara Reverted(reason)
```

`MinimumDuration` impede toggle-spam: M3 durante o primeiro segundo é ignorado
(com feedback), evitando que o jogador farme o VFX de ativação.

## 4. Formas do MVP

### Berserker — Swordsman

| Campo | Valor |
|---|---|
| Custo de ativação | 20 mana |
| Dreno | 4 mana/s |
| Damage | ×1.60 |
| Speed | ×1.15 |
| Defense | ×0.80 (recebe ~25% mais dano) |
| Visual | overlay emissivo vermelho + partículas nos ombros |
| Duração prática | ~20 s (com uso normal de habilidades) |

### Overdrive — Gunslinger

| Campo | Valor |
|---|---|
| Custo de ativação | 20 mana |
| Dreno | 5 mana/s |
| AttackSpeed (cadência) | ×1.50 |
| Speed | ×1.15 |
| Damage | ×1.10 |
| Defense | ×0.90 |
| Visual | overlay ciano + rastro de movimento |
| Duração prática | ~18 s |

## 5. Interação com outros sistemas

| Sistema | Interação |
|---|---|
| **Mana** | única fonte de custo; `Drain` parcial, nunca `TryConsume` |
| **Stats** | tudo via `PercentMult` com `Source = "form:<Id>"` |
| **Habilidades** | `RequiredTags`/`BlockedByTags` casam com `GrantedTags` |
| **Animação** | `AnimationSetOverride` troca o `AnimationLibrary` do `AnimationTree` |
| **Morte** | `Revert(Death)` antes de processar `Died` |
| **Pause** | dreno congela junto com `_PhysicsProcess` |
| **Nova onda** | forma **persiste** entre ondas (decisão de design: recompensa o gerenciamento de mana) |

## 6. HUD

Faixa inferior do HUD (ver [spec 11](11-ui-hud-e-menus.md)):

```
        ◀   Normal  |  [ BERSERKER ]  |  ???   ▶
                        4 mana/s
```

- Item selecionado destacado com `ThemeColor`.
- Forma **ativa** ganha borda pulsante + contador de dreno.
- Barra de mana muda de cor enquanto transformado, comunicando o dreno.
- Aviso visual quando a mana cai abaixo de 15% com forma ativa (reversão
  iminente).

## 7. Critérios de aceite (M4)

- [ ] Scroll troca a seleção sem gastar mana e sem alterar o personagem.
- [ ] M3 ativa, consome o custo e aplica os três multiplicadores.
- [ ] Mana drena visivelmente na barra e a reversão ocorre exatamente em 0.
- [ ] Após reverter, todos os stats voltam ao valor base **exato** (teste xUnit
      com epsilon).
- [ ] Morrer transformado não deixa modificador nem VFX órfão após respawn.
- [ ] Adicionar uma terceira forma é criar um `.tres` e referenciá-lo no
      `CharacterDefinition` — zero código.
