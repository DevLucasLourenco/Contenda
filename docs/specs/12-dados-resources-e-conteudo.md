# 12 — Dados, Resources e conteúdo

Se um número precisa ser ajustado por alguém que não vai recompilar, ele mora
num `.tres`.

## 1. Catálogo de arquivos

```
data/
├── characters/
│   ├── swordsman.tres              CharacterDefinition
│   ├── gunslinger.tres             CharacterDefinition
│   └── shared/
│       ├── health_swordsman.tres   HealthDefinition
│       ├── health_gunslinger.tres
│       ├── mana_swordsman.tres     ManaDefinition
│       ├── mana_gunslinger.tres
│       ├── movement_swordsman.tres MovementSettings
│       └── movement_gunslinger.tres
│
├── weapons/
│   ├── sword.tres                  WeaponDefinition
│   ├── revolver.tres               WeaponDefinition
│   └── combo/
│       ├── sword_slash_1.tres      MeleeComboStep
│       ├── sword_slash_2.tres
│       └── sword_slash_3.tres
│
├── abilities/
│   ├── swordsman/
│   │   ├── dash_slash.tres · rising_slash.tres
│   │   └── spin_slash.tres · heavy_lunge.tres
│   └── gunslinger/
│       ├── quick_step_shot.tres · explosive_shot.tres
│       └── fan_the_hammer.tres · deadeye.tres
│
├── transformations/
│   ├── berserker.tres              TransformationDefinition
│   └── overdrive.tres
│
├── enemies/
│   ├── grunt.tres · runner.tres · shooter.tres
│   ├── brute.tres · warlord.tres
│   └── shared/health_grunt.tres · movement_grunt.tres · ...
│
├── waves/
│   ├── waveset_default.tres        WaveSetDefinition
│   ├── wave_01.tres … wave_05.tres WaveDefinition
│   └── entries/                    EnemySpawnEntry
│
├── camera/
│   └── combat_camera.tres          CameraSettings
│
└── animation/
    ├── animset_swordsman.tres      AnimationSet
    ├── animset_gunslinger.tres
    ├── animset_berserker.tres
    └── animset_grunt.tres
```

## 2. Regras de autoria

1. **Todo `Resource` de conteúdo é `[GlobalClass]`** — aparece no menu
   "New Resource" do editor, sem precisar digitar o caminho do script.
2. **`Id` é `StringName`, minúsculo, com namespace por ponto:**
   `swordsman.dash_slash`, `form:berserker`, `enemy.grunt`.
   Ids são únicos globalmente e nunca mudam depois de commitados (saves e
   estatísticas dependem deles).
3. **Nada de referência circular** entre resources. `CharacterDefinition` aponta
   para habilidades; habilidades não apontam para personagens.
4. **Um `.tres` = um conceito.** Não empacotar quatro habilidades num arquivo só.
5. **`.tres` em texto**, nunca `.res` binário — sem diff em code review é
   impossível revisar balanceamento.
6. **Sem `PackedScene` inline.** Cenas referenciadas por caminho, sempre.

## 3. Validação em tempo de carga

`ContentValidator` roda no `GameBootstrap` (e como teste de CI) e falha alto:

| Verificação | Erro |
|---|---|
| dois `CharacterDefinition` com o mesmo `Id` | duplicate id |
| duas habilidades do mesmo personagem com a mesma `Sequence` | ambiguous sequence |
| `Sequence` vazia ou com > 4 tokens | invalid sequence |
| `ManaCost` > `MaxMana` do personagem | unusable ability |
| `AbilityDefinition.Kind == Projectile` sem `ProjectileScene` | missing scene |
| `WaveDefinition` com `Entries` vazio | empty wave |
| `EnemyDefinition` sem `Health` ou `Movement` | incomplete definition |
| `AnimationSet` referenciando clipe ausente no `.glb` | missing animation |
| `TransformationDefinition.ManaDrainPerSecond <= 0` | free transformation |

Falhar no boot é melhor do que descobrir em playtest. Em build de release,
o validador roda e só loga; em debug, ele **quebra**.

## 4. `AnimationSet`

Desacopla nomes de animação do código. Cada modelo de terceiro traz nomes
próprios (`mixamo.com`, `Idle_A`, `1H_Melee_Attack_Slice`); o código conhece
apenas papéis.

```csharp
[GlobalClass]
public partial class AnimationSet : Resource
{
    [Export] public StringName Idle;
    [Export] public StringName Walk;
    [Export] public StringName Run;
    [Export] public StringName Dash;
    [Export] public StringName Hit;
    [Export] public StringName Knockback;
    [Export] public StringName Death;

    [Export] public StringName[] BasicAttacks;   // combo, em ordem
    [Export] public Godot.Collections.Dictionary<StringName, StringName>
             AbilityAnimations;                  // abilityId -> clipe

    [Export] public float RunBlendSpeed = 5.5f;  // m/s no topo do blend
}
```

Trocar o modelo do Swordsman = trocar `ModelScene` + `AnimationSet`. Zero código.

## 5. Convenção de nomes

| Item | Padrão | Exemplo |
|---|---|---|
| arquivo `.tres` | `snake_case` | `dash_slash.tres` |
| `Id` de conteúdo | `snake_case` com namespace | `swordsman.dash_slash` |
| `Source` de modificador | `tipo:id` | `form:berserker` |
| classe C# | `PascalCase` | `AbilityDefinition` |
| cena `.tscn` | `PascalCase` | `CharacterSelectMenu.tscn` |
| ação de input | `snake_case` | `command_confirm` |
| animação | como veio do asset, mapeada no `AnimationSet` | — |

## 6. Fluxo para adicionar conteúdo

**Nova habilidade** (~5 min, sem código):

1. `data/abilities/<personagem>/<nome>.tres` → `AbilityDefinition`
2. preencher `Id`, `Sequence`, custos, `Kind` e parâmetros
3. mapear a animação em `AnimationSet.AbilityAnimations`
4. adicionar ao array `Abilities` do `CharacterDefinition`
5. rodar o jogo — o resolver e o HUD se atualizam sozinhos

**Novo inimigo** (~15 min): `.glb` → `AnimationSet` → `EnemyDefinition` →
entrada em uma `WaveDefinition` → `EnemyPool.Prewarm`.

**Novo personagem** (~1 h): modelo + `AnimationSet` + `WeaponDefinition` +
4 `AbilityDefinition` + 1 `TransformationDefinition` + `CharacterDefinition`
+ registro em `data/characters/roster.tres`. Nenhum arquivo `.cs` novo.

**Novo modo de jogo:** aí sim, uma classe `IGameMode` — é a única categoria de
conteúdo que exige código, por definição.

## 7. Versionamento e Git

- `.tres` são texto e vão para o repositório normalmente.
- Modelos `.glb`, texturas e áudio via **Git LFS** (`*.glb`, `*.png`, `*.ogg`,
  `*.wav`).
- `.godot/` e `.mono/` no `.gitignore`.
- `*.import` **são commitados** — sem eles, cada dev reimporta com UIDs
  diferentes e as cenas quebram.
- `*.uid` **também são commitados**, pela mesma razão. O Godot 4.4+ gera um
  `<script>.cs.uid` por script, contendo um identificador estável
  (`uid://b6vpyabthlppr`). Cenas referenciam scripts por esse identificador; se
  cada máquina gerar o seu, as referências quebram no primeiro `git pull`.
  Eles aparecem sozinhos no primeiro import — não crie à mão.
- Alteração de balanceamento é um commit próprio, com o antes/depois no corpo da
  mensagem. Balanceamento misturado com refactor é irrevisável.

## 8. Critérios de aceite

- [ ] Nenhum valor de balanceamento hardcoded em `.cs` (auditável por review).
- [ ] O validador quebra o boot ao introduzir uma sequência ambígua.
- [ ] Um diff de `.tres` é legível em pull request.
- [ ] Trocar `AnimationSet` troca todas as animações sem tocar em código.
