# 22: Um inimigo percebe o jogador, persegue e ataca com aviso

**What to build:** o primeiro oponente que reage. Ele nota o jogador a certa
distância, encara antes de partir, persegue, para ao chegar perto, avisa que vai
bater, bate, e descansa antes de bater de novo.

**Blocked by:** 07, 08

**Status:** done

- [x] O inimigo ignora o jogador até ele entrar no raio de percepção
- [x] Ele **não** persegue através de parede
- [x] Há um instante de alerta antes de partir para cima — o jogador vê que foi notado
- [x] O golpe tem aviso visível antes de causar dano: brilho (sem animação nem
      som -- ver Implementação sobre modelos/áudio ainda não existirem)
- [x] Depois de bater ele fica um tempo sem poder bater de novo
- [x] Apanhar o interrompe; ele se recompõe e volta a perseguir
- [x] Perder o jogador de vista não faz ele desistir na hora — só depois de um tempo
- [x] O inimigo usa **os mesmos** componentes de vida, atributos, movimento e
      combate que o jogador; nada de locomoção duplicada

## Comments

O aviso antes do golpe não é enfeite: com câmera fixa e vários inimigos em volta,
sem telegrafia o dano parece aleatório e o jogador culpa o jogo.

O inimigo produz o mesmo tipo de intenção por quadro que o jogador produz a
partir do teclado. É isso que faz o movimento ser um só no projeto inteiro — e é
o que vai permitir, sem código novo, que ele pule e avance com dash.

## Implementação

`EnemyBrain` substitui o `PlayerInputController` como fonte de
`IntentFrame` -- `CharacterController` agora tenta `_entrada?.Poll()` e,
faltando isso, `_cerebro?.Poll(delta)` (novo campo, novo `case` no switch de
registro). Nenhum outro componente sabe a diferença: `MovementComponent`,
`CombatComponent`, `HealthComponent`, `StatsComponent` são os MESMOS nós que
o jogador usa, sem nenhuma ramificação por quem está no controle.

A decisão pura de "qual o próximo estado" mora em `EnemyStateMachine` (POCO
novo, testado em xUnit): consome sinais já resolvidos (alvo visível, dentro
do alcance de ataque, golpe terminou) e não sabe nada de percepção,
navegação ou combate de verdade. `EnemyBrain` é quem produz esses sinais na
fronteira com a engine:

- **Percepção** (`Perception`, novo -- raio + `IntersectRay` contra
  `PhysicsLayers.World`, mesma técnica de `HitscanWeapon.AlcanceAteParede`):
  usa o raio de DETECÇÃO (22 m) enquanto ocioso, e o raio de PERDA, maior
  (30 m), depois de já ter percebido -- evita ioiô de estado bem na borda.
  "Não persegue através de parede" sai de graça: a MESMA malha de navegação
  da arena (`data/navmesh/arena_nav.tres`, já existente) faz o contorno
  físico, e o raycast de linha de visão faz o inimigo "esquecer" de quem
  não consegue ver.
- **Navegação** (`NavigationMotor`, novo -- embrulha `NavigationAgent3D`):
  só devolve a direção do próximo passo do caminho; quem anda de verdade é
  o `MovementComponent`, via `IntentFrame.Move`. Único caminho de locomoção
  do projeto, exatamente como a spec exige.
- **Telegrafia** (`AttackTelegraphComponent`, novo -- mesma técnica de
  `DamageFlashComponent`, `MaterialOverride` em vez do material
  compartilhado do `Mesh`): acende um brilho emissivo durante o windup
  (`AttackWindup`, 0,45 s -- maior que qualquer janela de acerto do
  jogador, de propósito, spec 09 §7) e apaga ao golpear de verdade ou ao
  ser interrompido.
- **Atordoamento**: `HealthComponent.Damaged` (já existia) aciona
  `EnemyStateMachine.RegistrarGolpeRecebido()`, que interrompe QUALQUER
  estado. `EnemyBrain` cancela o golpe em andamento (`CombatComponent.Cancel()`)
  e apaga o aviso visual no mesmo instante -- golpes seguidos REFRESCAM o
  atordoamento em vez de somar duração (mesma disciplina de nunca somar
  travas do `ActionLockSet`).

Uma peça nova de matemática pura: `CameraMath.WorldToMovement` (testada em
xUnit, inversa exata de `MovementToWorld` já existente). A IA decide para
onde ir em coordenadas de MUNDO ("na direção do jogador"), mas
`MovementComponent.Tick` sempre reaplica a rotação de câmera de 45° sobre
`IntentFrame.Move` -- sem desfazer essa rotação primeiro, o inimigo andaria
na diagonal errada. `EnemyBrain` lê o MESMO `CameraSettings` (`combat_camera.tres`)
que o jogador usa, via `MovementComponent.CameraReference`, para nunca
divergir se alguém reajustar o ângulo da câmera no editor.

`GameSession` ganhou `PlayerBody` (o próprio `CharacterController` se
registra ali quando `Team == Player`): um `EnemyBrain` que escaneasse
`GetNodesInGroup` a cada quadro de física alocaria por quadro, proibido
pelas convenções §5 -- uma referência cacheada, anunciada uma vez, não.

**Achado real de bug, descoberto ao escrever o probe**: a matemática do
windup do golpe (`_windupRestante`) e a de "quando entrei neste estado"
dependiam de detectar a TRANSIÇÃO Chase→Attack (`estadoAntes != Estado`) —
correto em si, mas o probe original media o tempo a partir de um estado
Chase HERDADO de uma fase de teste anterior (que já tinha percorrido o
Alerta antes), fazendo a transição para Ataque acontecer bem mais cedo do
que a contagem de quadros do teste assumia — o golpe real já tinha
conectado quando o teste ainda achava estar no meio do windup. Não era um
bug de produção: era o probe reaproveitando estado entre fases sem resetar
— mesma categoria de lição do `AerialCombatProbe` (ticket 19). Corrigido
chamando `EnemyBrain.ResetForSpawn()` no início de cada fase que precisa de
um ponto de partida limpo e previsível.

**Escopo deliberadamente menor que a spec 09 inteira** (que cobre o M5
inteiro): sem roster de 5 inimigos (só `grunt`), sem `EnemyPool`/reciclagem
(ticket 25), sem os 3 níveis da cidade (ticket 23 -- a arena de hoje já tem
obstáculos e malha de navegação, o suficiente para "não persegue através de
parede"), sem separação entre múltiplos inimigos (só existe um), sem LOD de
percepção a 5 Hz (otimização para 40 inimigos, não para 1). `EnemyDefinition`
ficou com só os campos que este ticket usa -- nada de `ModelScene`,
`AnimationSet`, `AttackKind`, `ScoreValue`, `IsElite`, `EliteTint`,
`StaggerResistance`, que pertencem a tickets futuros e ainda não têm
sistema nenhum para consumi-los.

Sem som nem animação de verdade: nenhum dos dois sistemas existe ainda no
projeto (`AudioDirector` é um autoload vazio; modelos chegam no ticket 34).
O brilho da telegrafia é o sinal real e implementável agora; o ticket 36
(M8) já lista telegrafia sonora e visual na própria spec.

Verificado por `EnemyBrainProbe` (headless, árvore real, `Arena.tscn` + um
`EnemyGrunt` novo): ignora o jogador fora do raio de detecção, não o
percebe através do Obstaculo1 mesmo dentro do raio, alerta e depois
persegue de verdade (a distância entre os dois efetivamente diminui),
ataca só dentro do alcance com o aviso visual acendendo ANTES do dano
(golpe não conecta durante o windup), entra em cooldown depois de golpear,
apanhar interrompe um golpe em andamento (aviso apaga, `CombatComponent.Cancel()`
é chamado) e o atordoamento termina sozinho, e perder o jogador de vista
por mais que a tolerância configurada faz o grunt desistir e voltar a
`Idle`.

### Code review

Dois achados de standards corrigidos: `EnemyBrain` alcançava `NavigationMotor`
e `AttackTelegraphComponent` por `NodePath`/`GetNodeOrNull`, contrariando o
próprio contrato documentado do `ICharacterComponent`/`CharacterContext`
("nenhum componente procura outro por caminho; tudo vem do
`CharacterContext`") — todo outro componente do projeto já segue essa regra.
Corrigido dando aos dois um lugar em `CharacterContext` (`NavigationMotor`,
`AttackTelegraph`), preenchido pelo `Registrar` do `CharacterController` na
MESMA passada que já resolve `Health`/`Movement`/etc — antes de qualquer
`Bind` rodar, o que é o motivo de `NavigationMotor.Bind`/`AttackTelegraphComponent.Bind`
ficarem vazios de propósito (autorregistro dentro do próprio `Bind` dependeria
da ordem entre nós irmãos, que a primeira passada não garante; ler em
`Configure`, a segunda passada, é seguro porque ela só roda depois que TODOS
os `Bind` já terminaram). `EnemyBrain` passou a ler os dois via
`_contexto.NavigationMotor`/`_contexto.AttackTelegraph`, e os `NodePath`
correspondentes saíram do componente e da cena.

Também corrigido: `EnemyBrain` assinava `HealthComponent.Damaged` no `Bind`
mas nunca desassinava (`_ExitTree` ausente) — violação direta da "regra sem
exceção" das convenções §3, e exatamente o vazamento de handler que a spec
15 §5 cita como bug previsível de pooling. `DamageFlashComponent`, que
assina o mesmo evento, já desassina em `_ExitTree`; `EnemyBrain` passou a
fazer o mesmo.

Um achado de standards registrado e conscientemente adiado: o switch de
`CharacterController.Registrar` já passava de "meia dúzia" de casos antes
deste ticket (o próprio comentário da classe prevê trocar para autorregistro
quando isso acontecesse) e ganhou mais dois aqui. Migrar os ~12 casos
existentes para autorregistro é uma refatoração maior que o escopo deste
ticket, e mexeria em TODOS os componentes do projeto de uma vez só — fica
para uma passada dedicada, não para ser feita de passagem aqui.

Regressão completa (305 testes xUnit, os 12 probes headless -- incluindo
`EnemyBrainProbe` -- `ExportRelease` e o checador anti-2D) reexecutada
depois dos ajustes de review; tudo verde.
