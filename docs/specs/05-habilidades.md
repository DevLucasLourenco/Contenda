# 05 — Habilidades

Meta arquitetural: **adicionar uma habilidade = criar um `.tres`**. Zero linhas
em `CharacterController` ou `Player`.

Filosofia emprestada do Gameplay Ability System (Unreal) e do GDAbilitySystem:
`Ability` composta de custo, requisito, cooldown, efeitos e tags. Implementamos
uma versão enxuta e 100% nossa; se depois aparecerem dezenas de buffs/debuffs,
evoluímos nessa direção sem reescrever o consumidor.

## 1. `AbilityDefinition`

```csharp
[GlobalClass]
public partial class AbilityDefinition : Resource
{
    // Identidade
    [Export] public StringName Id;            // "swordsman.dash_slash"
    [Export] public string DisplayName;       // "Dash Slash"
    [Export] public Texture2D Icon;
    [Export(PropertyHint.MultilineText)] public string Description;

    // Entrada
    [Export] public CommandDirection[] Sequence;   // [Up, Up]
    [Export] public bool RequiresConfirm = true;   // sempre M2 no MVP

    // Custos e gates
    [Export] public float ManaCost      = 15f;
    [Export] public float Cooldown      = 3f;
    [Export] public float CastTime      = 0.10f;   // wind-up
    [Export] public float RecoveryTime  = 0.25f;   // pós-golpe
    [Export] public StringName[] RequiredTags;     // ex: "form.berserker"
    [Export] public StringName[] BlockedByTags;

    // Efeito
    [Export] public AbilityEffectKind Kind;        // ver §3
    [Export] public float Damage        = 35f;
    [Export] public float Range         = 6f;
    [Export] public float Radius        = 2f;
    [Export] public float Angle         = 90f;     // cone, graus
    [Export] public float DashDistance  = 0f;
    [Export] public float Knockback     = 4f;
    [Export] public int   MaxTargets    = 0;       // 0 = ilimitado
    [Export] public PackedScene ProjectileScene;

    // Locks durante a execução
    [Export] public ActionLock LocksDuringCast = ActionLock.Abilities;
    [Export] public bool GrantsInvulnerability = false;

    // Apresentação
    [Export] public StringName AnimationName;
    [Export] public PackedScene CastVfx;
    [Export] public PackedScene HitVfx;
    [Export] public AudioStream CastSfx;
    [Export] public float CameraShake = 0.2f;
}
```

## 2. `AbilityComponent`

```csharp
public sealed class AbilityComponent : Node, ICharacterComponent
{
    public IReadOnlyList<AbilityDefinition> Abilities { get; }
    public AbilityDefinition Executing { get; }     // null se ocioso

    public AbilityAttemptResult TryExecute(AbilityDefinition ability);
    public float CooldownRemaining(StringName abilityId);
    public bool  IsReady(StringName abilityId);
    public void  CancelCurrent();                   // morte, stun, pause

    public event Action<AbilityDefinition> Executed;
    public event Action<AbilityDefinition, AbilityAttemptResult> Rejected;
    public event Action<StringName, float> CooldownStarted;
}

public enum AbilityAttemptResult
{
    Success, OnCooldown, NotEnoughMana, Blocked, AlreadyCasting, TagRequirement
}
```

### Ordem de validação (curto-circuito)

```
1. Executing != null                 -> AlreadyCasting
2. ActionLock contém Abilities       -> Blocked
3. cooldown ativo                    -> OnCooldown
4. tags requeridas ausentes          -> TagRequirement
5. tags bloqueadoras presentes       -> TagRequirement
6. Mana.CanConsume(ManaCost)         -> NotEnoughMana
7. Mana.TryConsume(...)              -> Success
```

Mana só é consumida **depois** de todos os outros gates passarem. `TryConsume` é
atômico; se falhar por corrida, nada acontece.

## 3. `IAbilityBehavior` — como o efeito acontece

`AbilityEffectKind` seleciona um comportamento registrado. Cada `Kind` é uma
classe pequena, testável, que recebe um `AbilityContext`.

```csharp
public enum AbilityEffectKind : byte
{
    MeleeArc,        // cone à frente — Spin Slash, Cyclone
    DashAttack,      // desloca e aplica dano no trajeto — Dash Slash, Heavy Lunge
    Uppercut,        // dano + lança o alvo para cima — Rising Slash
    HitscanShot,     // raycast instantâneo — Quick Step Shot, Deadeye
    HitscanBurst,    // N hitscans em sequência — Fan The Hammer
    Projectile,      // instancia ProjectileScene — Explosive Shot
    SelfBuff         // só aplica modificadores temporários (pós-MVP)
}
```

```csharp
public sealed class AbilityContext
{
    public CharacterContext Character { get; }
    public AbilityDefinition Definition { get; }
    public Vector3 Origin { get; }
    public Vector3 AimDirection { get; }
    public float ElapsedTime { get; }
}

public interface IAbilityBehavior
{
    void Begin(AbilityContext ctx);
    void Tick(AbilityContext ctx, float delta);   // durante CastTime + ativo
    void End(AbilityContext ctx, bool cancelled);
}
```

Registro em `AbilityBehaviorRegistry` (dicionário `Kind → factory`). Adicionar um
novo tipo de efeito é adicionar uma entrada — não tocar em `AbilityComponent`.

## 4. Timeline de execução

```
M2 confirmado
   |
   ├─ [0 .. CastTime]        wind-up   — anim de preparação, locks aplicados
   ├─ [CastTime .. hit]      ativo     — hitbox/raycast/dash resolvem aqui
   └─ [hit .. +Recovery]     recovery  — locks ainda ativos, sem hit
                                       — cooldown já começou a contar no início
```

- **Cooldown começa no início do cast**, não no fim. Consistente com o
  feedback do HUD.
- Cancelamento (morte, stun forte): `End(cancelled: true)`, mana **não** é
  devolvida, cooldown **é** mantido. Regra explícita para evitar exploit.
- Hitbox de habilidade é sempre um `Area3D` na camada `PlayerHitbox`, ativo
  apenas na janela — nunca um `Area3D` permanentemente ligado.

## 5. Catálogo do MVP

### Swordsman

| Sequência | Habilidade | Kind | Mana | CD | Dano | Alcance |
|---|---|---|---|---|---|---|
| `W W` + M2 | **Dash Slash** | DashAttack | 15 | 3.0 s | 35 | 6 m de dash |
| `S W` + M2 | **Rising Slash** | Uppercut | 20 | 5.0 s | 40 | 2.5 m |
| `A D` + M2 | **Spin Slash** | MeleeArc | 18 | 4.0 s | 28 × 360° | 3 m |
| `W A W` + M2 | **Heavy Lunge** | DashAttack | 30 | 8.0 s | 75 | 9 m |

### Gunslinger

| Sequência | Habilidade | Kind | Mana | CD | Dano | Alcance |
|---|---|---|---|---|---|---|
| `W W` + M2 | **Quick Step Shot** | DashAttack + Hitscan | 15 | 3.0 s | 25 | dash 4 m, tiro 14 m |
| `S W` + M2 | **Explosive Shot** | Projectile | 25 | 6.0 s | 55 em 3 m | 18 m |
| `A D` + M2 | **Fan The Hammer** | HitscanBurst | 20 | 5.0 s | 6 × 12 | 10 m, cone 30° |
| `W A W` + M2 | **Deadeye** | HitscanShot | 35 | 10.0 s | 120 perfurante | 30 m |

Sem colisão de prefixo dentro de cada personagem — `W A W` não colide com `W W`
porque o segundo token difere. Verificado em carga pelo resolver.

## 6. Cooldowns e HUD

`AbilityCooldown` é uma struct simples com `EndTime`; o HUD lê
`CooldownRemaining(id)` e desenha um radial/escurecimento na linha da habilidade.

Uma habilidade em cooldown continua **listada** no HUD (o jogador precisa
aprender a sequência), apenas esmaecida com o tempo restante.

## 7. Extensões previstas (não no MVP)

- `AbilityDefinition.GrantedTags` e efeitos com duração (base para buffs).
- Habilidades desbloqueadas por transformação (`TransformationDefinition
  .UnlockedAbilities` já existe — ver [spec 06](06-transformacoes.md)).
- Cancelamento de recovery por outra habilidade (cancel windows) — cria
  profundidade de combo real.

## 8. Critérios de aceite (M3)

- [ ] As 8 habilidades existem como `.tres` e nenhuma delas é referenciada por
      nome em código C#.
- [ ] Executar sem mana mostra feedback e não consome nada.
- [ ] Cooldown aparece no HUD e impede reexecução.
- [ ] Morrer durante o cast cancela sem deixar hitbox ativa órfã.
- [ ] Criar uma 9ª habilidade nova exige **apenas** um `.tres` e uma entrada na
      lista do `CharacterDefinition`.
