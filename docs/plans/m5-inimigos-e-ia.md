# M5 — Inimigos e IA

**Objetivo:** inimigos que percebem, navegam, atacam com telegrafia, morrem e
voltam ao pool — 40 deles a 60 fps.

**Esforço:** 7–9 dias · **Depende de:** M2 (paralelizável com M3/M4)

Spec: [09 — Inimigos e IA](../specs/09-inimigos-e-ia.md)

Milestone mais longo do MVP e o que mais costuma escorregar. O risco não é a
FSM — é performance e pooling.

## Entregáveis

- `EnemyController` reusando os componentes do jogador
- `EnemyBrain` com a FSM de 7 estados
- `NavigationMotor` sobre `NavigationAgent3D` com avoidance
- `Perception` com raio + linha de visão
- `EnemyDefinition` + 5 tipos em `.tres`
- `EnemyPool` com contrato de reciclagem testado
- Barras de vida sobre os inimigos
- Telegrafia de ataque

## Tarefas

### 1. Cena do inimigo

- [ ] `scenes/characters/Enemy.tscn` — derivada da estrutura do personagem
- [ ] Reusa `StatsComponent`, `HealthComponent`, `MovementComponent`,
      `CombatComponent` — **sem duplicar nenhum**
- [ ] `NavigationAgent3D` configurado (raio 0.5, altura 1.8, avoidance ligado)
- [ ] Hurtbox na camada `EnemyHurtbox`, hitbox em `EnemyHitbox`

### 2. `EnemyDefinition`

- [ ] `Resource` conforme spec 09 §5
- [ ] `data/enemies/`: `grunt`, `runner`, `shooter`, `brute`, `warlord`
- [ ] Valores da tabela da spec 09 §6
- [ ] `HealthDefinition` e `MovementSettings` por tipo

### 3. FSM

- [ ] `EnemyState` (enum de 7 valores)
- [ ] `EnemyBrain` — `switch` por estado, cada caso ≤ ~20 linhas
- [ ] Transições exatamente como o diagrama da spec 09 §2
- [ ] `Alert` de 0.4 s antes de perseguir (leitura para o jogador)
- [ ] `Staggered` entrando de qualquer estado por hitstun
- [ ] `Death` terminal, com 1.2 s antes do `Release`
- [ ] O brain **produz um `IntentFrame`**; não move o corpo diretamente

### 4. Percepção

- [ ] `Perception` — raio 22 m, `LoseTargetRadius` 30 m
- [ ] Linha de visão por raycast na camada `World`
- [ ] Histerese: só perde o alvo após 3 s fora do raio
- [ ] Roda a 5 Hz em `Idle`, 10 Hz em combate — nunca a 60

### 5. Navegação

- [ ] `NavigationMotor` com `RepathInterval` 0.25 s
- [ ] **Repath escalonado por fase** (offset por instância)
- [ ] `GetDesiredDirection()` alimentando o `IntentFrame`
- [ ] Avoidance RVO no `NavigationAgent3D`
- [ ] Força de separação adicional (< 1.2 m, peso 0.35)
- [ ] Fallback: sem caminho válido, anda direto para o alvo por 1 s e repatha

### 6. Ataque

- [ ] `EnemyAttackController` com `MeleeSwing`, `RangedShot`, `Charge`
- [ ] Windup ≥ 0.3 s em todos, com animação e emissivo pulsante
- [ ] Decal de área no chão para `brute` e `warlord`
- [ ] `ActionLock.Movement` durante o ataque
- [ ] `AttackCooldown` no estado `Recover`
- [ ] Dano ao jogador pelo mesmo pipeline do M2

### 7. Pool

- [ ] `Pool<T>` genérico em `Utils/`
- [ ] `EnemyPool` com `Prewarm`, `Acquire`, `Release`, `ActiveCount`
- [ ] **Contrato de reciclagem** — os 6 passos da spec 09 §8
- [ ] `ResetForSpawn()` em todos os componentes envolvidos
- [ ] Prewarm: grunt 60, runner 30, shooter 20, brute 8, warlord 1
- [ ] **Teste GdUnit4:** 100 ciclos de acquire/release deixam o estado idêntico

### 8. Apresentação

- [ ] `WorldHealthBar` — `Sprite3D` + `SubViewport`, aparece após o 1º dano,
      some 3 s depois
- [ ] Barra maior com nome para elites; barra no topo da tela para o boss
- [ ] Tint dourado nos elites
- [ ] Animação de morte + dissolve antes do `Release`

### 9. Performance

- [ ] Cenário de debug com 40 inimigos (`--bench=40enemies`)
- [ ] Profiler: confirmar IA ≤ 3 ms/frame
- [ ] LOD de IA: > 35 m pensa a 5 Hz
- [ ] `Area3D.Monitoring` desligado fora das janelas ativas
- [ ] Overlay `--stats` com inimigos ativos e frame time p99

## Critérios de aceite

- [ ] Inimigos contornam obstáculos em vez de encostar na parede
- [ ] **40 ativos ≥ 60 fps** na máquina de referência
- [ ] Nenhum inimigo persegue através de parede
- [ ] Reciclar 100× não altera comportamento nem vaza handler
- [ ] Todo ataque tem windup visível antes do dano
- [ ] A horda não vira pilha de corpos sobrepostos
- [ ] Um inimigo novo é só um `.tres` + assets
- [ ] Nenhum componente de movimento duplicado entre jogador e inimigo

## Riscos

| Risco | Mitigação |
|---|---|
| **Performance com 40 inimigos** | pooling, repath escalonado e LOD desde o início — não como otimização posterior |
| Amontoado ilegível | avoidance + separação; testar com 20 inimigos convergindo num ponto |
| Handler vazado no pooling | contrato explícito + teste de 100 ciclos |
| Inimigo preso na geometria | fallback de movimento direto + o safety de 5 s do `WaveDirector` (M6) |
| FSM virando espaguete | limite de ~20 linhas por estado; passou disso, vira classe |

## Go/no-go

**Pergunta:** 40 inimigos rodam a 60 fps?

Se não: reduzir o teto para 25 e ajustar o design das ondas no M6. Melhor um
jogo com 25 inimigos fluido do que 40 a 40 fps. **Não** entrar no M6 com o
problema de performance em aberto — ondas maiores só o amplificam.
