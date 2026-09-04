# M2 — Atributos e combate básico

**Objetivo:** vida, mana, stats e o ataque M1 dos dois personagens, batendo em
manequins. Sem habilidades, sem transformações, sem IA.

**Esforço:** 5–7 dias · **Depende de:** M1

Specs: [04 — Atributos](../specs/04-atributos-vida-mana-stats.md) ·
[07 — Combate](../specs/07-combate-armas-e-dano.md) ·
[08 — Personagens](../specs/08-personagens.md)

## Entregáveis

- `StatsComponent` com `StatBlock` e modificadores por fonte
- `HealthComponent` e `ManaComponent` completos
- `CharacterDefinition` + os dois `.tres` de personagem
- `IWeapon` com `SwordWeapon` (combo de 3) e `RevolverWeapon` (hitscan)
- `DamageInfo`, `IDamageable`, hitbox/hurtbox por camada
- Hitstop, knockback, flash de dano, números flutuantes
- Manequim de teste com vida e barra
- HUD provisório de barras

## Tarefas

### 1. Stats

- [ ] `StatId`, `ModifierOp`, `StatModifier` (POCO)
- [ ] `StatBlock` — POCO, com cache por stat e invalidação
- [ ] Ordem de aplicação `Flat → PercentAdd → PercentMult` (spec 04 §1)
- [ ] `StatsComponent` (`Node`) sobre o `StatBlock`
- [ ] `RemoveBySource` — a base da reversão de transformação
- [ ] **Testes xUnit:** adicionar e remover devolve o valor base exato

### 2. Vida

- [ ] `HealthDefinition` (`Resource`)
- [ ] `HealthState` (POCO) + `HealthComponent` (`Node`, `IDamageable`)
- [ ] Fila de dano resolvida num ponto único do frame
- [ ] Mitigação por `DefenseMultiplier`
- [ ] Morte **idempotente**
- [ ] i-frames (0.25 s no jogador)
- [ ] Reescala de `CurrentHealth` quando `MaxHealth` muda
- [ ] `ResetForSpawn()`
- [ ] **Testes:** dois golpes no mesmo frame disparam `Died` uma vez

### 3. Mana

- [ ] `ManaDefinition` (`Resource`)
- [ ] `ManaComponent` com `TryConsume` (atômico) e `Drain` (parcial)
- [ ] Regeneração com delay de 1 s após gasto
- [ ] Evento `Depleted`
- [ ] `ResetForSpawn()`
- [ ] **Testes:** `TryConsume` insuficiente não altera nada

### 4. Definições de personagem

- [ ] `CharacterDefinition` (`Resource`, `[GlobalClass]`)
- [ ] `data/characters/swordsman.tres` e `gunslinger.tres` com os valores da
      spec 08 §6
- [ ] `CharacterController` monta modelo, arma e componentes a partir do `.tres`
- [ ] Tecla de debug para alternar o personagem em runtime (acelera muito o
      teste dos dois arquétipos)

### 5. Armas

- [ ] `IWeapon`, `WeaponBase`, `WeaponDefinition`
- [ ] `MeleeComboStep` (`Resource`)
- [ ] `MeleeWeapon` — hitbox `Area3D` habilitada só na janela
- [ ] `SwordWeapon` — cadeia de 3 golpes, janela de combo, reset
- [ ] `HitscanWeapon` — raycast + spread + tracer
- [ ] `RevolverWeapon` — cilindro de 6, recarga automática
- [ ] `data/weapons/sword.tres`, `revolver.tres` e os 3 `MeleeComboStep`
- [ ] `WeaponSocket` via `BoneAttachment3D` (com primitivas por enquanto)
- [ ] Lista de alvos já atingidos por golpe (não bater duas vezes)

### 6. Dano

- [ ] `DamageInfo` (`readonly record struct`), `DamageType`, `Team`
- [ ] `HitboxEmitter` e `HurtboxComponent` nas camadas corretas
- [ ] Checagem de time em código, além das camadas de física
- [ ] Pipeline completo da spec 07 §6

### 7. Combate

- [ ] `CombatComponent` com `ActionLock` **por fonte e com duração**
- [ ] `RequestBasicAttack()` delegando à arma
- [ ] `ApplyHitstun`, `ApplyKnockback`
- [ ] `AttackSpeed` do `StatBlock` dividindo o `AttackInterval`

### 8. Game feel

- [ ] Hitstop local (0.04 s / 0.09 s) — **não** via `Engine.TimeScale`
- [ ] Knockback com decaimento em 0.25 s
- [ ] Flash emissivo de 0.08 s ao levar dano
- [ ] Números de dano flutuantes (`Label3D` billboard, pooled)
- [ ] Screen shake no acerto e ao levar dano

### 9. Alvo de teste

- [ ] `scenes/debug/TrainingDummy.tscn` — `HealthComponent` + hurtbox + barra
- [ ] 3 manequins na arena, um deles revidando (dano fixo em intervalo)
- [ ] Overlay de debug: DPS acumulado, último dano, tempo até matar

### 10. HUD provisório

- [ ] `HealthBar` e `ManaBar` com número + valor
- [ ] Camada de dano atrasada na barra de vida
- [ ] Sem estilização ainda — o HUD real é do M7

## Critérios de aceite

- [ ] M1 com espada encadeia 3 golpes com timing correto e reseta ao perder a janela
- [ ] M1 com revólver dispara, gasta munição, recarrega e acerta no ponto mirado
- [ ] Um golpe atinge cada manequim **uma vez**
- [ ] Levar dano reduz a barra, dispara i-frames e flash
- [ ] Gastar mana pausa a regeneração por 1 s
- [ ] Trocar `swordsman.tres` → `gunslinger.tres` muda tudo sem recompilar lógica
- [ ] Hitstop e knockback perceptíveis
- [ ] Cobertura xUnit ≥ 80% em `StatBlock`, `HealthState` e mana

## Riscos

| Risco | Mitigação |
|---|---|
| Janelas de hit desalinhadas com a animação | usar valores em `MeleeComboStep`, não tracks — permite ajustar sem reimportar |
| `ActionLock` travando o jogador para sempre | locks com duração e fonte; log de debug listando locks ativos |
| Combate "sem peso" | hitstop e shake **neste** milestone, não como polimento depois |

## Definição de pronto

Bater em manequim já é **satisfatório** — com feedback, peso e timing. Se não
for divertido aqui, não vai ficar melhor com 40 inimigos.
