# M3 — Comandos e habilidades

**Objetivo:** a mecânica assinatura funcionando — sequências de WASD + M2
executam habilidades data-driven, com o HUD mostrando o buffer ao vivo.

**Esforço:** 6–8 dias · **Depende de:** M2

Specs: [03 — Input e combos](../specs/03-input-comandos-e-combos.md) ·
[05 — Habilidades](../specs/05-habilidades.md)

> Este é o milestone mais importante do projeto. Ao fim dele existe o
> **go/no-go** da ideia central.

## Entregáveis

- `CommandBuffer` e `AbilityComboResolver` (POCOs, testados)
- `AbilityDefinition` + as 8 habilidades em `.tres`
- `AbilityComponent` com gates, cooldowns e cancelamento
- 5 comportamentos de efeito (`MeleeArc`, `DashAttack`, `Uppercut`,
  `HitscanShot`, `HitscanBurst`, `Projectile`)
- `AbilityGuide` no HUD com os 6 estados de linha
- Validação de conteúdo que quebra o boot em sequência ambígua

## Tarefas

### 1. Buffer de comandos

- [ ] `CommandDirection`, `CommandToken`
- [ ] `CommandBuffer` — POCO, ring buffer, `Capacity` 6,
      `TokenLifetime` 0.70 s, `SequenceTimeout` 1.20 s
- [ ] Push apenas na **borda de subida**; movimento continua lendo estado contínuo
- [ ] Limpeza em: execução bem-sucedida, M2 sem match, morte, stun, pause
- [ ] Evento `Changed` para o HUD
- [ ] **Testes xUnit:** expiração por token, timeout de sequência, capacidade,
      limpeza, sequência de 3 tokens dentro da janela

### 2. Resolver

- [ ] `AbilityComboResolver` com trie de prefixos
- [ ] `Resolve(seq)` — match exato no nó atual
- [ ] `Candidates(seq)` — habilidades com o prefixo, para o HUD
- [ ] **Lança em construção** se duas habilidades tiverem a mesma sequência
- [ ] Reconstrução ao adicionar habilidades de transformação (M4)
- [ ] **Testes:** exato, prefixo, sem match, ambiguidade, sequência vazia

### 3. Definição de habilidade

- [ ] `AbilityDefinition` (`Resource`, `[GlobalClass]`) — spec 05 §1
- [ ] `AbilityEffectKind`, `AbilityContext`, `IAbilityBehavior`
- [ ] `AbilityCooldown` (POCO)
- [ ] `AbilityAttemptResult`

### 4. Componente

- [ ] `AbilityComponent` com a ordem de validação da spec 05 §2
- [ ] Mana consumida **só depois** de todos os gates
- [ ] Timeline `CastTime → ativo → RecoveryTime`
- [ ] Cooldown iniciando no **começo** do cast
- [ ] `CancelCurrent()` desligando hitboxes ativas
- [ ] Locks vindos de `LocksDuringCast`
- [ ] Eventos `Executed` / `Rejected` / `CooldownStarted`

### 5. Comportamentos

- [ ] `AbilityBehaviorRegistry` (`Kind → factory`)
- [ ] `MeleeArcBehavior` — cone com `Radius`/`Angle`
- [ ] `DashAttackBehavior` — desloca `DashDistance` aplicando dano no trajeto
- [ ] `UppercutBehavior` — dano + impulso vertical no alvo
- [ ] `HitscanShotBehavior` — raycast, com opção perfurante (Deadeye)
- [ ] `HitscanBurstBehavior` — N tiros em cone, intervalados
- [ ] `ProjectileBehavior` + `Projectile.tscn` com explosão em raio
- [ ] Todos com `MaxTargets` e knockback

### 6. Conteúdo

- [ ] 4 `.tres` do Swordsman: `dash_slash`, `rising_slash`, `spin_slash`,
      `heavy_lunge`
- [ ] 4 `.tres` do Gunslinger: `quick_step_shot`, `explosive_shot`,
      `fan_the_hammer`, `deadeye`
- [ ] Valores exatos da spec 05 §5
- [ ] Referenciados nos `CharacterDefinition`

### 7. Validação

- [ ] `ContentValidator` com as verificações da spec 12 §3
- [ ] Chamado no `GameBootstrap`: quebra em debug, loga em release
- [ ] Rodando na CI

### 8. HUD da guia de combos

- [ ] `AbilityGuide.tscn` + `AbilityGuideRow.tscn`
- [ ] Linha gerada a partir dos `.tres` — nada hardcoded
- [ ] Os 6 estados de linha da spec 11 §2.1
- [ ] Assinar `CommandBuffer.Changed` para atualizar em tempo real
- [ ] Flash vermelho + som ao confirmar sem match
- [ ] Cooldown e custo de mana visíveis por linha

### 9. Integração

- [ ] Prioridade de input: M2 > M1 > M3 no mesmo frame
- [ ] `PlayerInputController` alimentando o buffer
- [ ] Habilidades funcionando contra os manequins do M2
- [ ] Debug: log de `[buffer] → [resolve] → [result]` em cada M2

## Critérios de aceite

- [ ] Andar segurando W por 3 s grava **1** token
- [ ] `W W` + M2 executa Dash Slash sem latência perceptível
- [ ] `W A W` + M2 executa Heavy Lunge e não colide com `W W`
- [ ] Sequência incompleta expira em ~1.2 s e o HUD volta ao neutro
- [ ] M2 sem match não dispara o ataque básico como fallback
- [ ] Sem mana: feedback claro, nada consumido
- [ ] Em cooldown: linha esmaecida com tempo restante
- [ ] Morrer durante o cast não deixa hitbox órfã
- [ ] Uma 9ª habilidade nova exige só um `.tres` + entrada no `CharacterDefinition`
- [ ] Duas habilidades com a mesma sequência quebram o boot com mensagem clara

## Sessão de tuning obrigatória

| Parâmetro | Testar |
|---|---|
| `TokenLifetime` | 0.5 / **0.7** / 0.9 s |
| `SequenceTimeout` | 1.0 / **1.2** / 1.5 s |
| `CastTime` das habilidades | 0.05 / **0.10** / 0.20 s |
| Tamanho das sequências | só 2 tokens vs 2 e 3 tokens |

Convidar alguém que nunca jogou e medir: **em quanto tempo executa uma
habilidade de propósito?** Se passar de 2 minutos, o HUD ou o timing estão
errados.

## Riscos

| Risco | Mitigação |
|---|---|
| **Sequências saem por acidente ao andar** | é o risco número 1; se acontecer, exigir toques em intervalo mínimo entre tokens (ex.: ignorar token se o anterior veio há < 60 ms com a mesma direção) |
| Sequências longas travam o jogador parado | manter tudo em 2–3 tokens |
| O jogador não entende o sistema | o HUD ao vivo é a solução — não é opcional |
| M2 confirmando a habilidade errada em prefixo | regra de match exato + validação de ambiguidade |

## Go/no-go — o mais importante do projeto

**Pergunta:** executar habilidades por sequência de WASD é **divertido**, ou é
um obstáculo entre o jogador e a ação?

Se for obstáculo, as alternativas em ordem de custo:

1. reduzir todas as sequências para 2 tokens;
2. aumentar as janelas de tempo;
3. **plano B:** teclas 1–4 disparam as habilidades, e as sequências viram um
   atalho opcional que dá bônus (dano ou custo reduzido).

O plano B custa pouco: `AbilityDefinition` já tem tudo, só muda a origem do
gatilho. Decidir **aqui**, não no M7.
