# 04: Arena de bloqueio vista pela câmera 2.5D

**What to build:** abrir o jogo passa a mostrar a arena 3D no ângulo definitivo
do jogo. Nada se move ainda, mas o enquadramento é o real — e ajustar o `.tres`
da câmera muda o resultado em runtime, sem recompilar.

**Blocked by:** 02.

**Status:** concluído

- [x] `Arena.tscn` com a estrutura da
      [spec 02 §9.2](../../../docs/specs/02-camera-e-mundo-25d.md): chão de
      ~60×60 m, 6–10 obstáculos, paredes de borda, `GroundPlane`, 8 marcadores de
      spawn e um `PlayerSpawn`
- [x] **uma plataforma elevada (+1.5 m) com rampa** — é a prova visual de que Y é
      altura real e não decoração
- [x] `NavigationRegion3D` com navmesh bakeada e commitada
      (`AgentRadius` 0.5, `AgentHeight` 1.8, `MaxSlope` 45°)
- [x] `DirectionalLight3D` com sombra e `WorldEnvironment` com SSAO leve
- [x] `CameraSettings` como `Resource` e `data/camera/combat_camera.tres` com
      pitch −55°, yaw 45°, FOV 38°, distância 14
- [x] `CameraRig` **irmão** do futuro jogador na árvore, nunca filho, com
      `CombatCamera` aplicando offset e ângulos a partir do resource
- [x] editar o `.tres` altera o enquadramento em runtime
- [x] nenhum nó 2D na cena

## Comments

Este ticket entrega o pilar que impede o projeto de derivar para 2D. Se o
resultado parecer "isométrico chapado", o sintoma é FOV alto demais ou distância
curta — teste a matriz de tuning do
[M1](../../../docs/plans/m1-movimento-e-camera.md) antes de aceitar os padrões.

---

**Concluído em 2026-09-04.** Verificado com o Godot 4.7.2 .NET instalado:

- a cena importa e roda sem erro; a navegação bakeia headless e está commitada
  em `data/navmesh/arena_nav.tres` (151 polígonos, agente raio 0,5 m, altura
  1,8 m, rampa máx 45°)
- as faixas de altura navegáveis provam que a rampa **conecta**: gradiente
  contínuo de 0,5 → 1,3 → 1,7 → 1,9 m entre o chão e o topo da plataforma
- 22 testes cobrem a matemática do enquadramento e a suavização

**Em aberto — decisão sua:** o go/no-go do M1. "A câmera 2.5D é legível e
agradável de jogar?" é julgamento humano e eu não posso fazê-lo. Rode
`godot --path . --scene res://scenes/debug/FramingCapture.tscn` para gerar a
imagem, ou simplesmente abra o projeto.

**Duas ressalvas registradas:**

1. Topos de obstáculo entram na navegação como ilhas inalcançáveis (38 vértices
   a 3,3 m). Inofensivas para o caminho, mas uma consulta de "ponto mais próximo
   na malha" poderia teleportar um inimigo para cima de uma caixa. Reavaliar no
   ticket 22.
2. O chão é uma superfície lisa sem textura. Movimento e profundidade vão ler
   mal até a cidade do ticket 21 — é limitação inerente ao bloqueio, não defeito.
