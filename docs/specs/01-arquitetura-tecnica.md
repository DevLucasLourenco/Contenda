# 01 — Arquitetura técnica

## 1. Stack

| Item | Escolha | Motivo |
|---|---|---|
| Engine | **Godot 4.7.x — edição .NET** | única edição que roda C# |
| Linguagem | **C# / .NET 8** | requisito do projeto; sem GDScript |
| Renderer | **Forward+** | luzes/sombras 3D decentes em desktop |
| Física | Godot Physics 3D (Jolt quando disponível) | suficiente para cápsulas + hitboxes |
| Plataformas | Windows, Linux, macOS | Web não é suportado com C# |
| Testes | **xUnit** (núcleo POCO) + **GdUnit4** (cenas) | ver [spec 15](15-qualidade-testes-e-performance.md) |
| Formato de asset | **glTF 2.0 (.glb)** | melhor caminho de import do Godot 4 |

## 2. Layout do repositório

```
Contenda/
├── Contenda.sln
├── Contenda.csproj              # projeto Godot .NET (raiz do project.godot)
├── project.godot
├── docs/
├── src/                         # todo o C# de gameplay
├── scenes/                      # .tscn
├── data/                        # .tres de conteúdo
├── assets/                      # modelos, texturas, áudio, fontes
├── tests/
│   ├── Contenda.Tests/          # xUnit — núcleo puro
│   └── Contenda.SceneTests/     # GdUnit4 — integração
├── tools/                       # scripts de build/CI
├── THIRD-PARTY-NOTICES.md
└── .github/workflows/ci.yml
```

> `src/` é apenas organização de arquivos; o Godot compila tudo que estiver no
> `.csproj`. `scenes/` espelha `src/` sempre que uma cena tem script.

## 3. Árvore de namespaces (`src/`)

Espelha o que foi desenhado na concepção, com os ajustes marcados.

```
src/
├── Core/
│   ├── GameBootstrap.cs         # autoload: inicializa serviços
│   ├── GameSession.cs           # autoload: escolha de modo/personagem, resultado
│   ├── SceneRouter.cs           # autoload: troca de cena assíncrona + loading
│   ├── GameConstants.cs         # camadas de física, nomes de ações, tags
│   ├── ServiceLocator.cs        # acesso tipado aos autoloads
│   └── ProjectInfo.cs           # nome/versão do build; canário do GodotSharp
│
├── Characters/
│   ├── Base/
│   │   ├── CharacterController.cs      # contêiner + cache de componentes
│   │   ├── CharacterContext.cs         # handle passado aos componentes
│   │   ├── CharacterDefinition.cs      # Resource: stats, arma, habilidades, formas
│   │   └── ICharacterComponent.cs
│   ├── Swordsman/
│   └── Gunslinger/
│
├── Components/
│   ├── Health/      HealthComponent.cs · HealthDefinition.cs
│   ├── Mana/        ManaComponent.cs · ManaDefinition.cs
│   ├── Stats/       StatBlock.cs · StatModifier.cs · StatsComponent.cs
│   ├── Movement/    MovementComponent.cs · MovementSettings.cs
│   ├── Combat/      CombatComponent.cs · DamageInfo.cs · IDamageable.cs
│   ├── Abilities/   AbilityComponent.cs · AbilityDefinition.cs
│   │                AbilityContext.cs · AbilityCooldown.cs · IAbilityBehavior.cs
│   ├── Transformations/ TransformationComponent.cs · TransformationDefinition.cs
│   │                    TransformationModifier.cs
│   ├── Targeting/   TargetingComponent.cs
│   └── Animation/   CharacterAnimator.cs · AnimationSet.cs
│
├── Input/
│   ├── PlayerInputController.cs   # Input do Godot -> intenções
│   ├── IntentFrame.cs             # struct com a intenção do frame
│   ├── CommandBuffer.cs
│   ├── CommandToken.cs
│   └── AbilityComboResolver.cs    # trie de sequências
│
├── Weapons/
│   ├── IWeapon.cs · WeaponBase.cs · WeaponDefinition.cs
│   ├── MeleeWeapon.cs · HitscanWeapon.cs
│   ├── SwordWeapon.cs · RevolverWeapon.cs
│   └── Hit/  HitboxEmitter.cs · HurtboxComponent.cs
│
├── AI/
│   ├── EnemyController.cs · EnemyDefinition.cs
│   ├── EnemyBrain.cs · EnemyState.cs
│   ├── NavigationMotor.cs · EnemyAttackController.cs
│   └── Perception.cs
│
├── GameModes/
│   ├── IGameMode.cs · GameModeBase.cs · GameModeResult.cs
│   └── Horde/  HordeGameMode.cs · WaveDirector.cs · WaveDefinition.cs
│                SpawnDirector.cs · EnemyPool.cs
│
├── Camera/          CameraRig.cs · CombatCamera.cs · CameraSettings.cs
│                 CameraMath.cs   # matemática pura do enquadramento, testável
│
├── Tools/           NavmeshBaker.cs · FramingCapture.cs
│                 # ferramentas de linha de comando; EXCLUÍDAS do export
│
├── UI/
│   ├── HUD/    HudController.cs · HealthBar.cs · ManaBar.cs
│   │           AbilityGuide.cs · TransformationSelector.cs
│   │           WaveBanner.cs · WorldHealthBar.cs
│   └── Menus/  MainMenu.cs · GameModeMenu.cs · CharacterSelectMenu.cs
│               SettingsMenu.cs · PauseMenu.cs · ResultsScreen.cs
│
├── Settings/        GameSettings.cs · SettingsStore.cs · InputRebinder.cs
└── Utils/           Ring.cs · MathX.cs · Pool.cs
```

**Ajustes em relação ao rascunho original**, com justificativa:

- `Components/Stats/` foi adicionado. Sem um `StatBlock` central, transformações
  teriam que alterar cada componente à mão — e reverter viraria bug garantido.
- `Components/Targeting/` foi promovido a componente porque a mira do Gunslinger
  depende de projetar o mouse no plano do chão.
- `Input/IntentFrame.cs` separa "ler teclado" de "decidir ação", o que permite
  reaproveitar o mesmo `MovementComponent` para a IA (que preenche o intent
  sozinha) — evita duplicar código de locomoção.

## 4. Modelo de componentes

### 4.1 Contrato

```csharp
public interface ICharacterComponent
{
    void Bind(CharacterContext ctx);          // resolve dependências
    void Configure(CharacterDefinition def);  // aplica dados do .tres
}
```

- Componentes são **`Node`s filhos** do `CharacterController` (aparecem no
  editor, aceitam `[Export]`, são inspecionáveis em runtime).
- O `CharacterController` faz **duas passadas** no `_Ready`:
  1. `Bind` em todos (todos já existem na árvore, ordem não importa);
  2. `Configure` em todos, na ordem: Stats → Health → Mana → Movement → Combat →
     Abilities → Transformations → Animation.
- Nenhum componente usa `GetNode("../Outro")`. Dependências vêm do
  `CharacterContext`.

### 4.2 `CharacterContext`

```csharp
public sealed class CharacterContext
{
    public CharacterController Owner { get; }
    public CharacterBody3D Body { get; }
    public StatsComponent Stats { get; }
    public HealthComponent Health { get; }
    public ManaComponent Mana { get; }
    public MovementComponent Movement { get; }
    public CombatComponent Combat { get; }
    public AbilityComponent Abilities { get; }
    public TransformationComponent Transformations { get; }
    public TargetingComponent Targeting { get; }
    public CharacterAnimator Animator { get; }
    public Node3D WeaponSocket { get; }
    public Team Team { get; }   // Player | Enemy | Neutral
}
```

Componentes opcionais podem ser `null` (um inimigo simples não tem
`TransformationComponent`). Todo consumo é defensivo.

### 4.3 O que **não** fazemos

- ❌ `Player.cs` com movimento + combate + UI + IA
- ❌ `if (character == Swordsman) ... else if (character == Gunslinger)`
- ❌ `SwordPlayer.cs` / `GunPlayer.cs` duplicando sistemas
- ❌ singletons estáticos mutáveis de gameplay (`GameManager.Instance.Player`)

A diferença entre personagens vive em **`CharacterDefinition.tres` + `IWeapon` +
lista de `AbilityDefinition`** — nunca em ramificação de código.

## 5. Comunicação entre sistemas

Três canais, cada um com uma regra clara:

| Canal | Quando usar | Exemplo |
|---|---|---|
| **Chamada direta** via `CharacterContext` | mesmo personagem, síncrono | `ctx.Mana.TryConsume(15f)` |
| **`event Action<T>` C#** | notificar mudança de estado do próprio componente | `Health.Damaged += OnDamaged` |
| **`GameEvents` (autoload)** | cross-cutting entre sistemas sem relação | `GameEvents.EnemyKilled` → score |

Regras:

- **Nunca** usar signal do Godot para lógica de gameplay entre componentes C# —
  perde tipagem e faz boxing em `Variant`. Signals ficam para wiring no editor.
- Todo `event` assinado em `_Ready` é **desassinado em `_ExitTree`**. Não há
  exceção: vazamento de handler no pooling de inimigos é o bug mais provável
  deste projeto.
- `GameEvents` só transporta **dados por valor** (`readonly record struct`),
  nunca referências a nós.

```csharp
public readonly record struct EnemyKilledEvent(
    StringName EnemyId, Vector3 Position, int ScoreValue);
```

## 6. Ordem de execução por frame

```
_PhysicsProcess (60 Hz fixo)
 1. PlayerInputController  -> IntentFrame (move, aim, attack, confirm, forma)
 2. CommandBuffer.Tick     -> expira tokens antigos
 3. AbilityComponent       -> consome confirm, resolve e executa
 4. TransformationComponent-> drena mana, reverte se necessário
 5. MovementComponent      -> aplica velocidade + MoveAndSlide
 6. CombatComponent        -> janelas de hit, cooldowns de arma
 7. AI (EnemyBrain)        -> decide estado, preenche IntentFrame do inimigo
 8. HealthComponent        -> processa a fila de dano do frame
_Process (variável)
 9. CameraRig              -> follow suavizado
10. HUD                    -> lê estado e desenha
```

Dano é **enfileirado** e aplicado num ponto único do frame, para que dois golpes
simultâneos não disparem `Died` duas vezes.

## 7. Autoloads

Registrados **nesta ordem** em Project Settings → Autoload. A ordem é a de
dependência, não a de importância: autoloads recebem `_Ready` na ordem de
registro, e `GameBootstrap` é o único que usa os outros — por isso vem por
último. Registrá-lo primeiro faz o `ServiceLocator` devolver nulo no boot.

| # | Autoload | Responsabilidade | Estado |
|---|---|---|---|
| 1 | `GameEvents` | barramento de eventos por valor | — |
| 2 | `GameSession` | modo escolhido, personagem escolhido, resultado da última partida | limpo ao voltar ao menu |
| 3 | `SceneRouter` | `GoTo(scenePath)` assíncrono com tela de loading | — |
| 4 | `AudioDirector` | buses, pooling de `AudioStreamPlayer3D` | — |
| 5 | `DamageNumberPool` | números de dano flutuantes, pooled (ticket 11) | — |
| 6 | `GameBootstrap` | aplica settings, valida conteúdo, abre a primeira cena | sem estado de partida |

Cada serviço que PRECISA ser encontrado por outro se **anuncia** ao
`ServiceLocator` no próprio `_Ready`; nada procura autoload por caminho literal
(`/root/GameSession`), o que quebraria em silêncio a cada renomeação.
`DamageNumberPool` é a exceção deliberada: ninguém segura referência a ele, só
ouve `GameEvents.DamageNumberRequested` sozinho — por isso fica fora da tabela
fixa do `ServiceLocator` (ver o comentário na própria classe).

Nenhum outro singleton além destes seis. Nada de `GameManager` genérico.

## 8. Camadas de física

Definidas em `GameConstants.cs` e no `project.godot` com os mesmos nomes.

| Bit | Camada | Ocupantes |
|---|---|---|
| 1 | `World` | chão, paredes, props estáticos |
| 2 | `PlayerBody` | cápsula do jogador |
| 3 | `EnemyBody` | cápsulas dos inimigos |
| 4 | `PlayerHitbox` | `Area3D` de golpes do jogador |
| 5 | `EnemyHitbox` | `Area3D` de golpes dos inimigos |
| 6 | `PlayerHurtbox` | volume que recebe dano do jogador |
| 7 | `EnemyHurtbox` | volume que recebe dano dos inimigos |
| 8 | `Projectile` | projéteis |
| 9 | `GroundPlane` | plano invisível Y=0 usado pelo raycast de mira |
| 10 | `Interactable` | pickups, futuros objetivos |

Máscaras: `PlayerHitbox` → `{EnemyHurtbox}`; `EnemyHitbox` → `{PlayerHurtbox}`;
corpos colidem com `World` e entre si com separação suave (ver
[spec 09](09-inimigos-e-ia.md)).

## 9. Cena do personagem (referência)

```
CharacterBody3D  [CharacterController]
├── CollisionShape3D            (CapsuleShape3D, r=0.4 h=1.8)
├── Model                       (Node3D — instância do .glb)
│   ├── Skeleton3D
│   └── MeshInstance3D
├── AnimationPlayer
├── AnimationTree               (StateMachine + blend de locomoção)
├── StatsComponent
├── HealthComponent
├── ManaComponent
├── MovementComponent
├── CombatComponent
├── AbilityComponent
├── TransformationComponent
├── TargetingComponent
├── CharacterAnimator
├── Hurtbox                     (Area3D)
├── WeaponSocket                (BoneAttachment3D na mão direita)
│   └── <arma instanciada em runtime>
└── FxRoot                      (Node3D — VFX de forma/hit)
```

A mesma cena base serve para inimigos, removendo `Abilities`,
`Transformations` e `Targeting`, e trocando `PlayerInputController` por
`EnemyBrain`.

## 10. Convenções obrigatórias

- Nada de `GetNode` com string mágica fora de `_Ready`, e sempre via
  `[Export] NodePath` ou `%UniqueName`.
- `[Export]` para tudo que um designer deve poder ajustar; `private` para o resto.
- `float` para tempo/dano; `double` só onde a API do Godot exigir.
- `StringName` para ids e nomes de ação — nunca `string` em hot path.
- Todo `Resource` de conteúdo é `[GlobalClass]`.
- Detalhes completos em [convenções de código](../plans/convencoes-de-codigo.md).
