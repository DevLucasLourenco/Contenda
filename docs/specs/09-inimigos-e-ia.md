# 09 — Inimigos e IA

## 1. Composição

Inimigos usam **os mesmos componentes** do jogador. A diferença é a origem do
`IntentFrame`: `EnemyBrain` em vez de `PlayerInputController`.

```
CharacterBody3D  [EnemyController]
├── CollisionShape3D
├── Model / Skeleton3D / AnimationPlayer / AnimationTree
├── NavigationAgent3D
├── StatsComponent
├── HealthComponent
├── MovementComponent          ← mesmo componente do jogador
├── CombatComponent            ← mesmo componente do jogador
├── EnemyBrain                 ← produz IntentFrame
├── NavigationMotor            ← traduz caminho em direção de movimento
├── EnemyAttackController
├── Perception
├── Hurtbox (Area3D)
├── WorldHealthBar (Sprite3D + SubViewport)
└── WeaponSocket
```

Nenhum `EnemyMovement.cs` duplicado. Se o movimento do jogador melhorar, o do
inimigo melhora junto.

## 2. Máquina de estados

```
        ┌──────┐
        │ Idle │ ◀────────────────────┐
        └──┬───┘                      │
   percebe │                          │ perde o alvo
           v                          │
        ┌───────┐                     │
        │ Alert │ ──── 0.4 s ────┐    │
        └───────┘                v    │
                             ┌───────┐│
                    ┌───────▶│ Chase ├┘
                    │        └───┬───┘
        fora de     │            │ dentro de AttackRange
        alcance     │            v
                    │        ┌────────┐
                    └────────┤ Attack │
                             └───┬────┘
                                 │ fim da animação
                                 v
                             ┌─────────┐
                             │ Recover │──▶ Chase
                             └─────────┘

   qualquer estado ── HP <= 0 ──▶ ┌───────┐
                                  │ Death │
                                  └───────┘
   qualquer estado ── hitstun ──▶ ┌──────────┐
                                  │ Staggered│──▶ Chase
                                  └──────────┘
```

```csharp
public enum EnemyState : byte
{ Idle, Alert, Chase, Attack, Recover, Staggered, Death }
```

`EnemyBrain` é um `switch` sobre o estado, cada caso com no máximo ~20 linhas.
Se passar disso, o estado vira uma classe.

### Regras por estado

| Estado | Comportamento |
|---|---|
| `Idle` | parado, animação idle, `Perception` roda a 5 Hz |
| `Alert` | vira para o alvo, animação de alerta, 0.4 s (dá tempo de leitura ao jogador) |
| `Chase` | `NavigationMotor` persegue; repath a cada 0.25 s ou se o alvo andou > 1.5 m |
| `Attack` | para, aplica `ActionLock.Movement`, dispara `EnemyAttackController` |
| `Recover` | cooldown pós-ataque (`AttackCooldown`), sem lock de rotação |
| `Staggered` | hitstun; sem ação; sai por timer |
| `Death` | anim de morte, colisão desligada, 1.2 s, depois volta ao pool |

## 3. `Perception`

- Raio de detecção (`DetectionRadius`, padrão 22 m), sem cone — em modo horda,
  cone de visão só gera inimigos parados atrás do jogador.
- Linha de visão via raycast na camada `World` (não persegue através de paredes).
- Uma vez em `Chase`, o alvo só é perdido se sair de `LoseTargetRadius` (30 m)
  por mais de 3 s — evita ioiô de estado.
- Alvo no MVP é sempre o jogador (`GameSession.PlayerBody`). A interface já
  aceita múltiplos alvos para PvP/co-op futuros.

## 4. `NavigationMotor`

```csharp
public sealed class NavigationMotor : Node
{
    [Export] public float RepathInterval = 0.25f;
    [Export] public float StoppingDistance = 0.4f;
    public void SetTarget(Vector3 worldPosition);
    public Vector3 GetDesiredDirection();   // alimenta o IntentFrame
}
```

- `NavigationAgent3D` com `AvoidanceEnabled = true` (RVO), `Radius` = 0.5,
  `MaxNeighbors` = 8, `NeighborDistance` = 4 m.
- O agente **não** move o corpo. Ele só devolve `GetNextPathPosition()`; a
  direção resultante entra no `IntentFrame.Move` e o `MovementComponent` faz o
  `MoveAndSlide`. Um único caminho de locomoção no projeto inteiro.
- Repath escalonado: cada inimigo tem um offset de fase para não recalcular
  todos no mesmo frame (com 40 inimigos isso é um pico visível).

### Separação (evitar o "amontoado")

Além da avoidance do agente, uma força de separação leve entre inimigos a
menos de 1.2 m, com peso 0.35. Sem isso, a horda vira uma bola de corpos
sobrepostos e o combate melee fica ilegível.

## 5. `EnemyDefinition`

```csharp
[GlobalClass]
public partial class EnemyDefinition : Resource
{
    [Export] public StringName Id;              // "grunt"
    [Export] public string DisplayName;
    [Export] public PackedScene ModelScene;
    [Export] public AnimationSet AnimationSet;

    [Export] public HealthDefinition Health;
    [Export] public MovementSettings Movement;

    [Export] public float AttackDamage    = 10f;
    [Export] public float AttackRange     = 2.0f;
    [Export] public float AttackWindup    = 0.45f;   // telegrafia
    [Export] public float AttackCooldown  = 1.4f;
    [Export] public EnemyAttackKind AttackKind;      // MeleeSwing | RangedShot | Charge

    [Export] public float DetectionRadius  = 22f;
    [Export] public float LoseTargetRadius = 30f;

    [Export] public int   ScoreValue = 10;
    [Export] public float StaggerResistance = 0f;    // 0..1
    [Export] public bool  IsElite = false;
    [Export] public Color EliteTint = Colors.Gold;
}
```

## 6. Roster do MVP

| Id | Papel | HP | Speed | Dano | Alcance | Windup | Score |
|---|---|---|---|---|---|---|---|
| `grunt` | massa, melee | 40 | 3.4 | 10 | 2.0 m | 0.45 s | 10 |
| `runner` | pressiona, rápido e frágil | 25 | 5.4 | 8 | 1.8 m | 0.30 s | 15 |
| `shooter` | força movimentação | 30 | 2.8 | 12 | 14 m | 0.70 s | 20 |
| `brute` | elite, tanque, knockback | 160 | 4.2 | 25 | 2.6 m | 0.80 s | 60 |
| `warlord` | boss da onda 5 | 900 | 3.0 | 35 | 3.0 m | 1.0 s | 500 |

`warlord` reusa `EnemyBrain` com 3 ataques alternados e uma investida — nenhuma
classe nova, só `EnemyAttackKind` + parâmetros.

## 7. Telegrafia — obrigatória

Todo ataque inimigo precisa ser legível antes de acertar:

| Sinal | Implementação |
|---|---|
| Windup animado | `AttackWindup` ≥ 0.3 s em todos |
| Cor | emissivo pulsando na cor do time durante o windup |
| Decal no chão | para `brute` e `warlord`, área do golpe projetada |
| Som | cue de wind-up distinto por tipo |

Com câmera fixa e horda, sem telegrafia o dano parece aleatório.

## 8. `EnemyPool`

Instanciar e destruir dezenas de inimigos por onda causa GC spikes e stutter.
Pooling desde o M5, não como otimização posterior.

```csharp
public sealed class EnemyPool : Node
{
    public void Prewarm(PackedScene scene, EnemyDefinition def, int count);
    public CharacterController Acquire(EnemyDefinition def, Vector3 position);
    public void Release(CharacterController enemy);
    public int ActiveCount { get; }
}
```

`Prewarm`/`Acquire` recebem a `PackedScene` explícita, não só a
`EnemyDefinition`: o roster completo do §5 (`ModelScene` na própria
definição) é dos tickets 26/29/34, que ainda não existem em código (ticket
25 implementa só `grunt`) -- até lá, identidade de cena e dados de
comportamento chegam por parâmetros separados, exatamente como qualquer
outra cena `EnemyGrunt.tscn`/`data/enemies/grunt.tres` já são hoje. Também
não existe `EnemyController`: um inimigo é um `CharacterController` comum
(§1), sem subclasse nenhuma.

```
Pool["grunt"]  → [ Enemy01, Enemy02, ..., Enemy60 ]  (inativos)
Spawn:  Acquire -> ResetForSpawn -> reposiciona -> ativa processo e colisão
Morte:  anim de morte -> DeathDuration -> Release -> desativa, esconde
```

### Contrato de reciclagem (fonte de bugs, então explícito)

Ao `Release`, o inimigo **obrigatoriamente**:

1. não deixa nenhum aviso de evento pendurado de uma vida anterior;
2. zera `HealthComponent`, `CombatComponent.Locks`, fila de dano, i-frames;
3. cancela habilidades/ataques em execução e remove hitboxes ativas;
4. reseta a "animação" de morte para o estado parado (hoje só um temporizador
   -- sem modelo/`AnimationPlayer` de verdade ainda, ADR-010; vira
   `AnimationTree` de fato no ticket 34);
5. desliga `ProcessMode`, `Visible`, `CollisionLayer` e `CollisionMask`;
6. remove modificadores de stat de qualquer fonte.

**Item 1 não é literal "desassinar no `Release`":** como `_ExitTree` nunca
roda para um nó pooled, `Bind`/`Configure` também nunca rodam de novo depois
do primeiro `_Ready` -- cada assinatura de evento (`Health.Damaged`,
`Health.Died`, ...) é feita UMA VEZ, no nascimento da instância, e dura pela
vida inteira do nó pooled, nunca desligada nem religada por `Release`/
`Acquire`. O que garante "nenhum aviso pendurado de uma vida anterior" não é
desassinar e reassinar, é `ResetForSpawn()` zerando só os DADOS que cada
assinatura consome -- o handler continua vivo e correto porque ele reage ao
estado atual, já resetado, não a um resquício da vida anterior.

`ResetForSpawn()` em cada componente cobre isso; um probe headless
(`EnemyPoolProbe`, ticket 25) verifica na árvore de nós real que um inimigo
reciclado 100 vezes tem exatamente o mesmo estado do recém-criado.

### Dimensionamento

| Tipo | Prewarm |
|---|---|
| `grunt` | 60 |
| `runner` | 30 |
| `shooter` | 20 |
| `brute` | 8 |
| `warlord` | 1 |

Total ~119 instâncias pré-alocadas, com **máximo de 40 ativas** simultâneas
(ver [spec 10](10-modos-de-jogo-horde.md)).

## 9. Otimizações previstas

| Técnica | Quando |
|---|---|
| Repath escalonado por fase | desde o M5 |
| LOD de IA: inimigos a > 35 m pensam a 5 Hz em vez de 60 | M5 |
| `AnimationTree` desligado fora do frustum | M9 |
| Hurtbox `Area3D` com `monitorable` só quando ativo | M5 |

## 10. Critérios de aceite (M5)

- [ ] Inimigos contornam obstáculos em vez de encostar na parede.
- [ ] 40 inimigos ativos mantêm ≥ 60 fps na máquina de referência.
- [ ] Nenhum inimigo persegue através de parede.
- [ ] Reciclar um inimigo 100× não altera comportamento nem vaza handler.
- [ ] Todo ataque tem windup visível antes do dano.
- [ ] A horda não vira uma pilha de corpos sobrepostos sobre o jogador.
