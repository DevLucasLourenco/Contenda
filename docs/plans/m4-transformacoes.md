# M4 — Transformações

**Objetivo:** scroll seleciona, M3 ativa, a mana drena continuamente e a
reversão é automática ao zerar.

**Esforço:** 3–4 dias · **Depende de:** M2 (stats/mana), M3 (habilidades)

Spec: [06 — Transformações](../specs/06-transformacoes.md)

Milestone curto porque o `StatBlock` do M2 já fez o trabalho pesado. Se estiver
levando mais de 4 dias, o `StatBlock` está errado.

## Entregáveis

- `TransformationDefinition` + `berserker.tres` e `overdrive.tres`
- `TransformationComponent` com seleção, ativação, dreno e reversão
- `TransformationSelector` no HUD
- VFX/material de forma ativa (placeholder aceitável)

## Tarefas

### 1. Definição

- [ ] `TransformationDefinition` (`Resource`, `[GlobalClass]`) — spec 06 §2
- [ ] `RevertReason` (`Manual`, `ManaDepleted`, `Death`, `ModeReset`)
- [ ] `data/transformations/berserker.tres`
      (20 mana, 4/s, dano ×1.6, speed ×1.15, defesa ×0.8)
- [ ] `data/transformations/overdrive.tres`
      (20 mana, 5/s, cadência ×1.5, speed ×1.15, dano ×1.1, defesa ×0.9)
- [ ] Referenciar nos `CharacterDefinition`

### 2. Componente

- [ ] `TransformationComponent` — spec 06 §3
- [ ] Lista sempre começando em `Normal` (índice 0)
- [ ] `SelectNext` / `SelectPrevious` com wrap-around
- [ ] `TryActivateSelected()` seguindo os 9 passos da ativação
- [ ] Modificadores com `Source = "form:<Id>"`, sempre `PercentMult`
- [ ] Dreno em `_PhysicsProcess` via `Mana.Drain` (parcial, nunca `TryConsume`)
- [ ] Reversão em 6 passos, incluindo `Stats.RemoveBySource`
- [ ] `MinimumDuration` de 1 s contra toggle-spam
- [ ] Reversão ao morrer, **antes** de processar `Died`
- [ ] `ResetForSpawn()`

### 3. Input

- [ ] `form_prev` / `form_next` no scroll → `FormScrollDelta` no `IntentFrame`
- [ ] `form_activate` (M3) → `FormActivatePressed`
- [ ] Opção "inverter scroll" lida das configurações (stub até o M7)
- [ ] Scroll ignorado durante `ActionLock.Forms`

### 4. Habilidades desbloqueadas

- [ ] `UnlockedAbilities` somam ao `AbilityComponent` na ativação
- [ ] `AbilityComboResolver` reconstruído (a trie precisa incluí-las)
- [ ] Removidas na reversão; se uma delas estiver executando, é cancelada
- [ ] `GrantedTags` registradas para `RequiredTags` / `BlockedByTags`
- [ ] No MVP nenhuma forma desbloqueia habilidade — mas o caminho fica testado
      com uma habilidade de teste temporária

### 5. Apresentação

- [ ] `MaterialOverlay` — emissivo aplicado ao `MeshInstance3D` (guardar e
      restaurar o material original)
- [ ] `ActivationVfx` (`GPUParticles3D` one-shot)
- [ ] `PersistentVfx` (aura), removido na reversão
- [ ] `ActivationSfx`
- [ ] Barra de mana muda para a `ThemeColor` da forma
- [ ] Pulso de alerta na barra abaixo de 15% com forma ativa

### 6. HUD

- [ ] `TransformationSelector.tscn` — faixa horizontal
- [ ] Slots: `Normal | BERSERKER | ???`
- [ ] Selecionado com moldura; ativo com borda pulsante
- [ ] Dreno por segundo exibido sob a forma ativa
- [ ] Animação lateral de 0.12 s ao trocar a seleção

### 7. Testes

- [ ] **xUnit:** ativar e reverter devolve todos os stats ao base **exato**
- [ ] **xUnit:** dreno de 4/s a partir de 100 esgota em 25 s e dispara reversão
- [ ] **xUnit:** ativar sem mana suficiente não altera nada
- [ ] **GdUnit4:** morrer transformado não deixa modificador nem VFX órfão

## Critérios de aceite

- [ ] Scroll troca a seleção sem custo e sem alterar o personagem
- [ ] M3 ativa, consome 20 de mana e aplica os multiplicadores
- [ ] A barra de mana drena visivelmente e a reversão acontece em 0
- [ ] Transformado, o dano de M1 e das habilidades sobe de forma mensurável
      (verificável no overlay de DPS do M2)
- [ ] Overdrive aumenta a cadência do revólver sem alterar `revolver.tres`
- [ ] M3 durante o primeiro segundo é ignorado com feedback
- [ ] Após reverter, os stats voltam ao valor base exato
- [ ] Adicionar uma terceira forma é criar um `.tres` — zero código

## Tuning

| Parâmetro | Testar | Efeito |
|---|---|---|
| Dreno | 3 / **4** / 6 /s | duração prática da forma |
| Custo de ativação | 15 / **20** / 30 | frequência de uso |
| `DamageMultiplier` | 1.4 / **1.6** / 2.0 | quão transformador é |
| `DefenseMultiplier` | 0.7 / **0.8** / 0.9 | risco assumido |

**Sensação alvo:** transformar-se é uma decisão, não um hábito. Se o jogador
fica transformado o tempo todo, o dreno está barato. Se nunca transforma, o
custo de ativação está caro ou o bônus é fraco.

## Riscos

| Risco | Mitigação |
|---|---|
| Stat não volta ao base | `RemoveBySource` + teste de igualdade exata; nunca reverter somando o inverso |
| Material original perdido | guardar referência na ativação, restaurar na reversão |
| Forma ativa após respawn | `ResetForSpawn` remove tudo por fonte |
| Jogador não percebe que está transformado | overlay + aura + cor da barra de mana — três canais |
