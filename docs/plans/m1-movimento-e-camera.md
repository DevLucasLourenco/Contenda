# M1 — Movimento e câmera 2.5D

**Objetivo:** uma cápsula andando com WASD numa arena 3D, sob a câmera fixa
inclinada. É o milestone que **prova que o jogo é 3D** e trava essa decisão.

**Esforço:** 4–5 dias · **Depende de:** M0

Spec de referência: [02 — Câmera e mundo 2.5D](../specs/02-camera-e-mundo-25d.md)

## Entregáveis

- `Arena.tscn` jogável com chão, obstáculos, rampa, plataforma e navmesh bakeada
- `CharacterController` mínimo + `MovementComponent` + `TargetingComponent`
- `CameraRig` + `CombatCamera` com `CameraSettings.tres`
- Movimento relativo à câmera funcionando em todas as direções
- Personagem encarando o cursor do mouse

## Tarefas

### 1. Arena de teste

- [ ] `scenes/arena/Arena.tscn` conforme a estrutura da spec 02 §9.2
- [ ] Chão ~60×60 m (`MeshInstance3D` + `StaticBody3D`)
- [ ] 6–10 obstáculos (caixas/colunas) para forçar pathfinding depois
- [ ] **1 plataforma elevada (+1.5 m) com rampa** — prova visual de que Y é real
- [ ] Paredes invisíveis nas bordas (`Bounds`)
- [ ] `GroundPlane` (`Area3D` na camada 9, Y=0)
- [ ] 8 `Marker3D` em `SpawnPoints` + 1 `PlayerSpawn`
- [ ] `DirectionalLight3D` com sombra + `WorldEnvironment` com SSAO leve
- [ ] `NavigationRegion3D` com navmesh bakeada e commitada

### 2. Câmera

- [ ] `src/Camera/CameraSettings.cs` (`Resource`, `[GlobalClass]`)
- [ ] `data/camera/combat_camera.tres` com pitch -55°, yaw 45°, FOV 38°,
      distância 14
- [ ] `src/Camera/CameraRig.cs` — `Node3D` com `SetTarget(Node3D)`
- [ ] `src/Camera/CombatCamera.cs` — aplica offset/pitch/yaw a partir do resource
- [ ] Follow com **`SmoothDamp`**, não `Lerp(a, b, delta)`
- [ ] Dead zone e look-ahead conforme spec 02 §6
- [ ] Screen shake aditivo (API pronta, usada só no M2)
- [ ] Hierarquia: `CameraRig` **irmão** do jogador, nunca filho

### 3. Personagem mínimo

- [ ] `scenes/characters/Character.tscn` — `CharacterBody3D` + `CollisionShape3D`
      (cápsula r=0.4 h=1.8) + placeholder `MeshInstance3D`
- [ ] `src/Characters/Base/CharacterController.cs` com o ciclo `Bind` →
      `Configure`
- [ ] `src/Characters/Base/CharacterContext.cs`
- [ ] `src/Characters/Base/ICharacterComponent.cs`
- [ ] Um marcador visual de "frente" no mesh (cone/seta) — sem ele é impossível
      julgar a rotação

### 4. Movimento

- [ ] `src/Components/Movement/MovementSettings.cs` + `.tres` de teste
- [ ] `src/Components/Movement/MovementComponent.cs`
- [ ] Consome `IntentFrame.Move`; **não** lê `Input` diretamente
- [ ] Conversão para espaço da câmera com o yaw do `CameraSettings`:

```csharp
var basis = new Basis(Vector3.Up, Mathf.DegToRad(camSettings.YawDegrees));
Vector3 dir = (basis * new Vector3(intent.Move.X, 0, intent.Move.Y)).Normalized();
```

- [ ] Aceleração/desaceleração (não velocidade instantânea)
- [ ] Gravidade + `MoveAndSlide`
- [ ] Rotação suave (`RotationSpeed`) para a direção alvo

### 5. Input e mira

- [ ] `src/Input/IntentFrame.cs`
- [ ] `src/Input/PlayerInputController.cs` — produz um `IntentFrame` por tick de
      física, lendo `Input.GetVector` e a posição do mouse
- [ ] `src/Components/Targeting/TargetingComponent.cs` — raycast do mouse contra
      o plano na altura do torso (spec 02 §7)
- [ ] `FaceMode.Aim` como padrão; `FaceMode.Movement` selecionável para comparar
- [ ] Debug: gizmo desenhando `AimPoint` no chão

### 6. Verificação anti-2D

- [ ] Script/`grep` que falha se aparecer `Node2D|CharacterBody2D|Camera2D|Sprite2D`
      em `scenes/` fora de `scenes/ui/`
- [ ] Adicionar essa verificação ao CI

## Critérios de aceite

- [ ] `W` move para **cima na tela** em qualquer ponto da arena
- [ ] `A`/`D` movem para os lados da tela, não do mundo
- [ ] Girar o personagem 360° não altera o enquadramento
- [ ] Subir na plataforma é visualmente perceptível (a altura lê)
- [ ] Sem jitter parado; sem sensação de borracha em corrida
- [ ] Alterar `combat_camera.tres` reflete em runtime
- [ ] O verificador anti-2D roda no CI e passa
- [ ] Nenhum componente usa `GetNode("../…")`

## Sessão de tuning obrigatória

Antes de fechar o milestone, testar e escolher — não aceitar os valores padrão
sem comparar:

| Parâmetro | Testar |
|---|---|
| Pitch | -50° / **-55°** / -60° |
| FOV | 35° / **38°** / 42° |
| Distância | 11 / **14** / 18 m |
| `FollowSmoothTime` | 0.10 / **0.16** / 0.24 |
| `FaceMode` | **Aim** vs Movement |

Registrar a escolha e o porquê no PR. Esses cinco números definem a identidade
visual do jogo mais do que qualquer asset.

## Riscos

| Risco | Mitigação |
|---|---|
| A câmera "parece isométrica chapada" | é o sintoma de FOV alto demais ou distância curta; testar a matriz acima |
| Movimento relativo à câmera com sinal errado | testar as 8 direções sistematicamente; `S` deve ir para baixo na tela |
| Enjoo/desconforto com look-ahead | `LookAheadFactor = 0` é um padrão seguro |

## Go/no-go

**Pergunta:** a câmera 2.5D é legível e agradável de jogar?
Se não, ajustar aqui — refazer a câmera depois do M5 é dez vezes mais caro.
