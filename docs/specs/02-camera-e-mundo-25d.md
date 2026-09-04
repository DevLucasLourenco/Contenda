# 02 — Câmera e mundo 2.5D

Esta spec existe por um motivo específico: **impedir que o projeto derive para 2D
de novo**. Qualquer PR que introduza um nó 2D no gameplay viola este documento.

## 1. Eixos e convenções de mundo

| Eixo | Significado |
|---|---|
| **X** | horizontal (esquerda/direita do mundo) |
| **Y** | **altura** — real, usada em pulos, desníveis, arcos de projétil |
| **Z** | profundidade |

- O jogador se move no **plano X/Z**. `Y` sofre gravidade.
- "Frente" do personagem = `-Basis.Z` (convenção Godot).
- Unidade: **1 unidade = 1 metro**. Personagem ≈ 1.8 m de altura.

## 2. Nós permitidos e proibidos

**Obrigatórios no gameplay:**

`CharacterBody3D` · `Camera3D` · `NavigationRegion3D` · `NavigationAgent3D` ·
`MeshInstance3D` · `StaticBody3D` · `Area3D` · `CollisionShape3D` ·
`DirectionalLight3D` · `WorldEnvironment` · `GPUParticles3D`

**Proibidos no gameplay (falha de review):**

`Node2D` · `CharacterBody2D` · `Camera2D` · `Sprite2D` · `Area2D` ·
`CollisionShape2D` · `TileMap` · `AnimatedSprite2D`

`Control`/`CanvasLayer` são permitidos **exclusivamente** para UI/HUD.
Barras de vida sobre inimigos usam `Sprite3D` com `SubViewport`, ou um `Control`
posicionado por `Camera3D.UnprojectPosition` — nunca `Node2D` no mundo.

## 3. Configuração da câmera

Projeção **perspectiva**, não ortográfica. Ortográfica achata a percepção de
profundidade e empurra o resultado para "isométrico chapado"; o requisito é
profundidade real.

| Parâmetro | Valor inicial | Faixa aceitável |
|---|---|---|
| Projection | `Perspective` | fixa |
| FOV | **38°** | 35–42° |
| Pitch (rotação X) | **-55°** | -50° a -60° |
| Yaw (rotação Y) | **45°** | 35–45° |
| Roll (rotação Z) | **0°** | fixo |
| Distância ao alvo | **14 m** | 11–18 m |
| Near / Far | 0.5 / 120 | — |
| Rotação em runtime | **TRAVADA** | — |

FOV baixo + distância alta = compressão de perspectiva. É isso que dá o
"aspecto 2.5D" mantendo paralaxe e altura legíveis.

```
              CAMERA
                 \
                  \   pitch -55°
                   \
                    ↓
                  Player
        ──────────────────────────
                 ARENA 3D
```

Ortográfica fica como **experimento pós-MVP** (`CameraSettings.UseOrthogonal`),
não como caminho padrão.

## 4. Hierarquia — a câmera **não** é filha do jogador

Errado (a rotação do player contamina a câmera):

```
Player
└── Camera3D
```

Correto:

```
Arena
├── Player
├── Enemies
└── CameraRig            [CameraRig]      — Node3D na origem
    └── CameraPivot      (Node3D)          — aplica yaw fixo
        └── Camera3D     [CombatCamera]    — offset local + pitch fixo
```

`CameraRig` recebe **apenas `TargetPosition`**. Nunca `TargetRotation`.

| Evento | Câmera |
|---|---|
| player gira | **não** gira |
| player ataca | **não** gira |
| player corre | acompanha a posição |
| player morre | continua viva e controlável pelo rig (para a tela de resultado) |

## 5. `CameraSettings` (Resource)

```csharp
[GlobalClass]
public partial class CameraSettings : Resource
{
    [Export] public float Fov = 38f;
    [Export] public float PitchDegrees = -55f;
    [Export] public float YawDegrees = 45f;
    [Export] public float Distance = 14f;
    [Export] public Vector3 TargetOffset = new(0, 1.0f, 0); // mira no torso
    [Export] public float FollowSmoothTime = 0.16f;         // SmoothDamp
    [Export] public float DeadZoneRadius = 0.35f;           // m — evita jitter
    [Export] public float LookAheadFactor = 0.20f;          // 0 = desligado
    [Export] public float MaxLookAhead = 2.5f;              // m
    [Export] public bool  UseOrthogonal = false;            // experimento
    [Export] public float OrthogonalSize = 16f;
    [Export] public float ShakeDecay = 6f;
}
```

## 6. Comportamento de follow

Roda em `_Process` (visual, não física), com `SmoothDamp` — não `Lerp` por
delta, que produz velocidade dependente de framerate.

```
desired = player.GlobalPosition + TargetOffset
        + clamp(aimDir * LookAheadFactor * dist(player, cursor), MaxLookAhead)

if (distance(current, desired) < DeadZoneRadius) desired = current;

rig.GlobalPosition = SmoothDamp(rig.GlobalPosition, desired,
                                ref velocity, FollowSmoothTime, dt);
```

Regras adicionais:

- **Sem colisão de câmera.** A arena do MVP não tem teto nem paredes altas entre
  câmera e jogador. Se um cenário futuro precisar, a solução é *fade* do
  obstáculo (material dithered), **não** mover a câmera.
- **Limites da arena (opcional):** `CameraRig` pode ser clampado a um `Aabb` da
  arena para não mostrar o vazio na borda.
- **Screen shake** aditivo em espaço local da câmera, decaindo por
  `ShakeDecay`; nunca altera a rotação base.
- Pause congela o follow (`_Process` respeita `ProcessMode`).

## 7. Mira e o plano do chão

O `TargetingComponent` converte a posição do mouse em um ponto no mundo:

1. `camera.ProjectRayOrigin(mouse)` + `camera.ProjectRayNormal(mouse)`
2. Interseção com o **plano Y = altura do torso do jogador** (não Y=0 — evita o
   erro de mira crescer com a distância em terreno plano).
3. Resultado é `AimPoint`; `AimDirection = normalize(aimPoint - playerPos)` com
   `Y` zerado.

O personagem **encara o `AimDirection`** por padrão
(`MovementSettings.FaceMode = Aim`). Alternativa configurável:
`FaceMode = Movement` (encara a direção do WASD). Justificativa do padrão: com
câmera fixa e um personagem à distância (Gunslinger), mirar com o mouse é o que
torna o alcance uma vantagem real.

Fallback para gamepad: `AimDirection` vem do analógico direito; sem input,
mantém a última direção válida.

## 8. Movimento relativo à câmera

**Requisito não negociável:** WASD é relativo à **câmera**, não ao mundo.

Com yaw de 45°, apertar `W` deve mover "para cima na tela", não na diagonal.

```csharp
var yaw = Mathf.DegToRad(settings.YawDegrees);
var basis = new Basis(Vector3.Up, yaw);
Vector3 worldDir = (basis * new Vector3(input.X, 0, input.Y)).Normalized();
```

O yaw usado é o do **`CameraSettings`**, lido uma vez e cacheado — nunca a
rotação instantânea do nó (que pode estar em shake).

## 9. A arena

### 9.1 Requisitos

| Item | MVP |
|---|---|
| Tamanho jogável | ~60 × 60 m, formato aproximadamente circular/octogonal |
| Bordas | paredes `StaticBody3D` intransponíveis, altura ≥ 3 m |
| Obstáculos | 6–10 blocos/colunas para forçar pathfinding e cobertura |
| Desnível | 1 plataforma elevada (+1.5 m) com rampa — prova que Y é real |
| Pontos de spawn | 8 `Marker3D` na periferia, agrupados em `SpawnPoints` |
| Iluminação | `DirectionalLight3D` com sombra + `WorldEnvironment` com SSAO leve |

### 9.2 Estrutura de cena

```
Arena.tscn
├── WorldEnvironment
├── DirectionalLight3D
├── NavigationRegion3D
│   └── Geometry           (MeshInstance3D + StaticBody3D do chão e obstáculos)
├── Bounds                 (StaticBody3D — paredes invisíveis)
├── GroundPlane            (Area3D na camada GroundPlane, Y=0, para raycast)
├── SpawnPoints            (Marker3D × 8)
├── PlayerSpawn            (Marker3D)
├── CameraRig
└── GameMode               (nó do IGameMode instanciado pelo SceneRouter)
```

### 9.3 Navmesh

- `NavigationRegion3D` com `NavigationMesh` **bakeada no editor** e commitada.
- `AgentRadius` = 0.5, `AgentHeight` = 1.8, `MaxSlope` = 45°.
- Rebake obrigatório ao mover geometria — checklist do PR.
- Sem navmesh dinâmica no MVP.

## 10. Critérios de aceite (M1)

- [ ] Nenhum nó 2D fora de `CanvasLayer`/UI (verificável por `grep` no `.tscn`).
- [ ] `W` move o personagem para cima na tela em qualquer posição da arena.
- [ ] Girar o personagem 360° não altera um único pixel do enquadramento.
- [ ] Subir na plataforma elevada é visualmente perceptível (a altura lê).
- [ ] Follow sem jitter parado e sem "borracha" em movimento rápido.
- [ ] Alterar `CameraSettings.tres` no editor reflete em runtime sem recompilar.
