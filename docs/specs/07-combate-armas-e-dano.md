# 07 — Combate, armas e dano

## 1. O princípio do M1

**M1 não é "soco".** M1 é `BasicAttack` — uma intenção. A arma equipada decide o
que isso significa.

```
M1 ──> CombatComponent.RequestBasicAttack()
          └─> ctx.Weapon.BasicAttack(ctx)
                 ├─ SwordWeapon    -> combo Slash 1 → 2 → 3
                 └─ RevolverWeapon -> Fire() (hitscan + recarga)
```

O mesmo input produz comportamentos completamente diferentes **sem um único
`if` sobre o personagem**. Essa abstração é o que permite adicionar o 3º, 10º e
50º personagem depois.

## 2. `IWeapon`

```csharp
public interface IWeapon
{
    WeaponDefinition Definition { get; }
    bool  CanAttack { get; }
    void  Equip(CharacterContext ctx, Node3D socket);
    void  Unequip();
    void  BasicAttack(CharacterContext ctx);
    void  Tick(float delta);
    event Action<int> ComboStepChanged;   // -1 = combo encerrado
}
```

Hierarquia:

```
WeaponBase (Node3D, IWeapon)
├── MeleeWeapon      — hitbox Area3D em janela de animação
│   └── SwordWeapon  — cadeia de 3 golpes
└── HitscanWeapon    — raycast instantâneo + tracer
    └── RevolverWeapon — cilindro de 6, recarga
```

## 3. `WeaponDefinition`

```csharp
[GlobalClass]
public partial class WeaponDefinition : Resource
{
    [Export] public StringName Id;
    [Export] public string DisplayName;
    [Export] public PackedScene ModelScene;        // .glb da arma
    [Export] public WeaponKind Kind;               // Melee | Hitscan | Projectile

    [Export] public float BaseDamage      = 20f;
    [Export] public float AttackInterval  = 0.45f; // s entre golpes
    [Export] public float Range           = 2.2f;
    [Export] public float Knockback       = 2f;

    // Melee
    [Export] public MeleeComboStep[] ComboSteps;

    // Hitscan
    [Export] public int   MagazineSize    = 6;
    [Export] public float ReloadTime      = 1.6f;
    [Export] public float SpreadDegrees   = 1.5f;
    [Export] public int   PelletsPerShot  = 1;
    [Export] public PackedScene TracerScene;

    [Export] public AudioStream AttackSfx;
    [Export] public AudioStream ImpactSfx;
}

[GlobalClass]
public partial class MeleeComboStep : Resource
{
    [Export] public StringName AnimationName;
    [Export] public float DamageMultiplier = 1f;
    [Export] public float HitWindowStart   = 0.18f;  // s desde o início da anim
    [Export] public float HitWindowEnd     = 0.32f;
    [Export] public float ComboWindowEnd   = 0.75f;  // até quando aceita o próximo M1
    [Export] public float ForwardStep      = 0.6f;   // m de avanço
    [Export] public float KnockbackMultiplier = 1f;
}
```

## 4. Espada — combo de 3

```
M1 -> Slash 1  (×1.00, 20 dano)
        |  M1 dentro da ComboWindow
        v
      Slash 2  (×1.10, 22 dano)
        |  M1 dentro da ComboWindow
        v
      Slash 3  (×1.60, 32 dano, knockback 2×)
        |  janela expira
        v
      volta ao passo 1
```

- Cada golpe avança o personagem `ForwardStep` metros na direção de mira —
  ataque melee que não desloca fica ruim de acertar com câmera fixa.
- A hitbox é um `Area3D` (cone/box) filho do `WeaponSocket`, **habilitado só
  entre `HitWindowStart` e `HitWindowEnd`**.
- Um alvo só pode ser atingido **uma vez por golpe** — lista de `InstanceId` já
  atingidos, limpa a cada novo passo do combo.
- Perder a janela reinicia em Slash 1. Não há penalidade além do reset.

## 5. Revólver — hitscan

```
M1 -> Fire()
      ├─ munição == 0 ? -> Reload() automático
      ├─ raycast a partir do cano na direção de mira
      │     spread aleatório dentro de SpreadDegrees
      ├─ primeiro corpo na máscara EnemyHurtbox recebe DamageInfo
      ├─ tracer visual origem → ponto de impacto (ou fim do alcance)
      └─ decrementa cilindro; muzzle flash; recuo de câmera leve
```

- `AttackInterval` de 0.30 s; segurar M1 dispara em cadência (semi-auto
  automático — segurar é confortável em modo horda).
- Cilindro de 6, recarga de 1.6 s, cancelável por habilidade (não por movimento).
- `AttackSpeed` do `StatBlock` divide o `AttackInterval` — é assim que Overdrive
  dá +50% de cadência sem tocar na arma.
- Sem queda de projétil; hitscan puro. Explosive Shot é o único projétil real.

## 6. `DamageInfo` e `IDamageable`

```csharp
public readonly record struct DamageInfo(
    float      Amount,
    DamageType Type,          // Physical | Explosive | True
    Vector3    HitPoint,
    Vector3    Direction,     // normalizada, para knockback e VFX
    float      Knockback,
    ulong      SourceId,      // InstanceId de quem causou
    StringName SourceTag,     // "weapon.sword", "ability.dash_slash"
    bool       IsCritical);

public interface IDamageable
{
    void ApplyDamage(in DamageInfo info);
    bool IsAlive { get; }
    Team Team { get; }
}
```

### Pipeline de dano

```
1. Hitbox detecta Hurtbox (camadas garantem que só o time inimigo entra)
2. Monta DamageInfo:
      Amount = base × Stats.Get(DamageMultiplier) × comboStep.DamageMultiplier
3. Alvo.ApplyDamage -> enfileira no HealthComponent
4. Ponto único do frame:
      final = Amount / max(0.1, alvo.Stats.Get(DefenseMultiplier))
      CurrentHealth -= final
      dispara Damaged; se <= 0, dispara Died
5. Efeitos colaterais: hitstop, número flutuante, VFX de impacto, knockback,
   screen shake, som
```

### Friendly fire

Desligado. `Hitbox.Team != Hurtbox.Team` é pré-condição — garantido pelas
camadas de física, e checado de novo em código (defesa em profundidade, porque
uma camada mal configurada no editor é fácil de introduzir).

## 7. Feedback de impacto (game feel)

Não é polimento opcional — é o que separa "funciona" de "é bom".

| Efeito | Valor inicial |
|---|---|
| **Hitstop** (congela ambos) | 0.04 s golpe normal · 0.09 s finalizador |
| **Knockback** | impulso na `Direction`, decaindo em 0.25 s |
| **Flash de dano** | material emissivo branco por 0.08 s |
| **Screen shake** | 0.15 ao acertar · 0.3 ao levar dano |
| **Número flutuante** | `Label3D` billboard, sobe 1 m e some em 0.6 s |
| **Slow-mo na morte do boss** | `Engine.TimeScale = 0.35` por 1.2 s |

Hitstop é implementado por escala de tempo **local** dos dois envolvidos (pausa
do `AnimationTree` + suspensão do movimento), não por `Engine.TimeScale` — senão
o resto da horda congela junto.

## 8. `CombatComponent`

```csharp
public sealed class CombatComponent : Node, ICharacterComponent
{
    public IWeapon Weapon { get; }
    public ActionLock Locks { get; }
    public bool IsAttacking { get; }

    public void EquipWeapon(WeaponDefinition def);
    public void RequestBasicAttack();
    public void ApplyLock(ActionLock flags, float duration, StringName source);
    public void ClearLock(StringName source);
    public void ApplyHitstun(float duration);
    public void ApplyKnockback(Vector3 impulse);
}

[Flags]
public enum ActionLock : byte
{
    None = 0, Movement = 1, Rotation = 2,
    BasicAttack = 4, Abilities = 8, Forms = 16,
    All = 31
}
```

Locks são **por fonte e com duração**, somados por OR. Isso evita o bug clássico
de "duas fontes travaram o movimento, uma liberou, o jogador destravou cedo".

## 9. Projéteis

`Projectile.tscn` — `Area3D` + `MeshInstance3D` + `GPUParticles3D`:

```csharp
public partial class Projectile : Area3D
{
    [Export] public float Speed = 22f;
    [Export] public float LifeTime = 3f;
    [Export] public float ExplosionRadius = 0f;   // 0 = impacto direto
    public DamageInfo Payload;
}
```

- Move-se em `_PhysicsProcess`; monitora colisão com `World` e `*Hurtbox`.
- Pooled junto com os inimigos (mesma infra de `Pool<T>`).
- Explosivos usam `PhysicsDirectSpaceState3D.IntersectShape` com esfera.

## 10. Critérios de aceite (M2)

- [ ] M1 com espada encadeia 3 golpes com timing e reseta ao perder a janela.
- [ ] M1 com revólver dispara, gasta munição e recarrega ao esvaziar.
- [ ] Um golpe atinge cada inimigo **uma única vez**.
- [ ] Trocar a arma equipada no `.tres` muda o comportamento do M1 sem
      recompilar lógica de personagem.
- [ ] Hitstop e flash de dano são perceptíveis em playtest.
- [ ] Nenhum dano ocorre entre entidades do mesmo time.
