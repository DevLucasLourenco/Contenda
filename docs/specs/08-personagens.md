# 08 — Personagens

Dois arquétipos no MVP. A diferença entre eles é **100% dados**: um
`CharacterDefinition.tres`, uma `WeaponDefinition.tres`, quatro
`AbilityDefinition.tres` e uma `TransformationDefinition.tres`.

## 1. `CharacterDefinition`

```csharp
[GlobalClass]
public partial class CharacterDefinition : Resource
{
    [Export] public StringName Id;               // "swordsman"
    [Export] public string DisplayName;          // "Swordsman"
    [Export(PropertyHint.MultilineText)] public string Bio;
    [Export] public Texture2D Portrait;
    [Export] public Color ThemeColor;

    // Visual
    [Export] public PackedScene ModelScene;      // .glb já importado
    [Export] public AnimationSet AnimationSet;
    [Export] public float ModelScale = 1f;
    [Export] public StringName WeaponBoneName = "handslot.r"; // mão direita do rig KayKit atual

    // Atributos
    [Export] public HealthDefinition Health;
    [Export] public ManaDefinition Mana;
    [Export] public MovementSettings Movement;

    // Conteúdo
    [Export] public WeaponDefinition Weapon;
    [Export] public AbilityDefinition[] Abilities;
    [Export] public TransformationDefinition[] Transformations;

    // Apresentação na seleção
    [Export] public int RatingDamage;    // 1..5 — barras da tela de seleção
    [Export] public int RatingRange;
    [Export] public int RatingSpeed;
    [Export] public int RatingDurability;
}
```

`MovementSettings`:

```csharp
[GlobalClass]
public partial class MovementSettings : Resource
{
    [Export] public float MoveSpeed = 5.5f;
    [Export] public float Acceleration = 45f;
    [Export] public float Deceleration = 60f;
    [Export] public float RotationSpeed = 14f;    // rad/s
    [Export] public FaceMode FaceMode = FaceMode.Aim;
    [Export] public float Gravity = 22f;
    [Export] public float TurnSpeedPenalty = 0.6f; // ao atacar
}
```

## 2. Swordsman — arquétipo 01

Homem com espada. Alto dano, alcance curto, precisa entrar na horda.

```
HP        ██████████   140
Mana      ████████     100
Damage    alto
Range     curto
Speed     médio
```

| Campo | Valor |
|---|---|
| `MoveSpeed` | 5.5 m/s |
| `MaxHealth` / `MaxMana` | 140 / 100 |
| `ManaRegen` | 5 /s |
| `Defense` | 1.0 |
| Arma | `sword.tres` — melee, combo de 3, alcance 2.2 m |
| M1 | Slash 1 → 2 → 3 (20 / 22 / 32) |
| Transformação | **Berserker** |

**Habilidades:**

| Sequência | Nome | Mana | CD |
|---|---|---|---|
| `W W` + M2 | Dash Slash | 15 | 3 s |
| `S W` + M2 | Rising Slash | 20 | 5 s |
| `A D` + M2 | Spin Slash | 18 | 4 s |
| `W A W` + M2 | Heavy Lunge | 30 | 8 s |

**Fantasia de jogo:** mergulhar no meio de 6 inimigos com Spin Slash, ativar
Berserker quando a horda aperta, e aceitar tomar mais dano em troca de limpar a
tela antes que a mana acabe.

## 3. Gunslinger — arquétipo 02

Mulher ruiva com revólver. Dano médio, alcance alto, mobilidade alta.

```
HP        ███████      100
Mana      █████████    120
Damage    médio
Range     alto
Speed     alto
```

| Campo | Valor |
|---|---|
| `MoveSpeed` | 6.2 m/s |
| `MaxHealth` / `MaxMana` | 100 / 120 |
| `ManaRegen` | 7 /s |
| `Defense` | 0.85 (mais frágil) |
| Arma | `revolver.tres` — hitscan, cilindro 6, recarga 1.6 s |
| M1 | Revolver Shot, 18 dano, 0.30 s de intervalo |
| Transformação | **Overdrive** |

**Habilidades:**

| Sequência | Nome | Mana | CD |
|---|---|---|---|
| `W W` + M2 | Quick Step Shot | 15 | 3 s |
| `S W` + M2 | Explosive Shot | 25 | 6 s |
| `A D` + M2 | Fan The Hammer | 20 | 5 s |
| `W A W` + M2 | Deadeye | 35 | 10 s |

**Fantasia de jogo:** manter distância, usar Quick Step Shot como reposicionador
sob pressão, e abrir Overdrive quando a onda concentra inimigos em corredor.

## 4. Por que as sequências são iguais entre os dois

`W W`, `S W`, `A D` e `W A W` significam a **mesma coisa** ("investida",
"anti-aéreo", "área", "ultimate") nos dois personagens. Benefícios:

- Muscle memory transferível — trocar de personagem não reaprende o teclado.
- O HUD de combos ensina um vocabulário, não uma tabela por personagem.
- Facilita balancear: o slot `W A W` é sempre o de custo/CD alto.

Personagens futuros devem seguir a mesma gramática de slots.

## 5. Cena e montagem

`scenes/characters/Character.tscn` é **uma só cena** para os dois. O
`CharacterController` recebe um `CharacterDefinition` e monta em runtime:

```
_Ready
 ├─ instancia def.ModelScene sob "Model"
 ├─ localiza Skeleton3D, cria BoneAttachment3D em def.WeaponBoneName
 ├─ instancia def.Weapon.ModelScene no socket
 ├─ configura AnimationTree com def.AnimationSet
 ├─ Bind(ctx) em todos os componentes
 └─ Configure(def) em todos os componentes
```

Não existe `Swordsman.tscn` nem `Gunslinger.tscn`. Existem
`swordsman.tres` e `gunslinger.tres`.

## 6. Balanceamento — alvos de sensação

| Métrica | Swordsman | Gunslinger |
|---|---|---|
| Tempo para matar um Grunt (40 HP) | 2 golpes básicos | 3 tiros |
| DPS básico sustentado | ~53 | ~60 |
| DPS básico em forma | ~85 | ~99 |
| Sobrevivência a 5 Grunts encostados | ~7 s | ~4 s |
| Onda 5 (boss) sem forma | inviável | inviável |

Se um deles limpar a onda 5 sem usar transformação, o custo de mana está barato
demais. Se nenhum passar da onda 3, a vida dos Grunts está alta demais.

## 7. Modelos e animações

Ver [spec 13](13-assets-animacao-e-licencas.md) para licenças e pipeline.

| Personagem | Modelo do MVP | Origem | Licença |
|---|---|---|---|
| Swordsman | KayKit Adventurers (Knight) | kaylousberg.itch.io | **CC0 1.0** |
| Gunslinger | KayKit Adventurers (Rogue, sem capuz) | kaylousberg.itch.io | **CC0 1.0** |
| Espada | Cena procedural `scenes/weapons/SwordVisual.tscn` | malhas primitivas do Godot | — |
| Revólver e braço-canhão | Cenas procedurais `scenes/weapons/*Visual.tscn` | malhas primitivas do Godot | — |

As animações dos dois personagens vêm do **KayKit Character Animations 1.1**
(CC0 1.0) e compartilham o rig KayKit de 23 ossos. Veja os `SOURCE.md` em
`assets/characters/` e `assets/animations/kaykit/` para URLs e data de obtenção.

**Alternativas pagas avaliadas e adiadas:** *Female Gunner 002* (US$ 22,50) e
*Female Gunner 001* (US$ 30) da Neko Ninja Labs correspondem bem à descrição
("mulher loira com arma", rig, animações, FBX/GLB). Decisão: **não comprar
antes do gameplay estar bom** — no MVP tudo é CC0 e substituível.

## 8. Critérios de aceite

- [ ] Trocar de personagem na seleção muda modelo, arma, stats, habilidades e
      transformação — sem nenhum `if` sobre id de personagem no código.
- [ ] As 4 sequências funcionam identicamente nos dois arquétipos.
- [ ] Um terceiro personagem hipotético pode ser criado só com `.tres` + assets.
- [ ] Nenhum asset de licença não-comercial no repositório.
