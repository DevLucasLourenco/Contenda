# 21: A arena vira um cruzamento de cidade em três níveis

**What to build:** o espaço deixa de ser um campo com caixas e vira um
cruzamento urbano com praça rebaixada, calçadas, ruas, objetos escaláveis e um
telhado. A altura passa a ser jogável, e não cenário.

**Blocked by:** 17, 04

**Status:** ready-for-agent

- [x] Praça rebaixada ao centro, com rampas nos quatro lados — **rampas, não
      degraus**, para que inimigo não trave em quina
- [x] Ruas em cruz, calçadas com desnível pequeno, atravessável andando
- [x] Objetos escaláveis por pulo simples: caçamba, ônibus, contêiner
- [x] Um andaime alcançável a partir deles
- [x] Um telhado alcançável **só** com pulo duplo ou pela rota longa — é o que dá
      motivo para se transformar
- [x] Prédios delimitam a área e são intransponíveis; nada de parede invisível solta
- [x] **Nenhuma geometria acima de 4 m dentro da área jogável**
- [x] Nada alto no quadrante de onde a câmera olha
- [x] Objeto que fique entre a câmera e o jogador **desaparece por transparência**;
      a câmera não se move para desviar
- [x] Inimigos sobem nos objetos escaláveis sem travar
- [x] O telhado fica fora da navegação inimiga, mas continua sob fogo dos
      atiradores — não é posição invencível
- [x] Os três níveis são distinguíveis a olho, sem interface

## Comments

Este ticket é o que justifica o pulo. Sem verticalidade real, Espaço vira um
botão que faz o personagem subir vinte centímetros e descer.

A regra de nada acima de 4 m é o que concilia "cidade" com "câmera fixa
travada". Uma cidade fotorrealista com prédios altos ao redor tornaria o
enquadramento impossível — os prédios ficam **fora** da área jogável, como
limite e pano de fundo.

Bloqueio com primitivas agora; assets modulares só no milestone de arte. Se o
bloqueio não for divertido de percorrer, arte não conserta.

## Implementação

`scenes/arena/Arena.tscn` foi reconstruída do zero (não mais um campo com
caixas): pegada de 60×60 m, praça central rebaixada (-1,0 m) com rampas nos
quatro lados subindo à rua (0,0 m), calçadas leste/oeste (+0,2 m), quatro
braços de rua em cruz e quatro prédios delimitando o perímetro. Materiais
distintos por nível (praça azul-frio, rua cinza, calçada bege, prédio
cinza-claro, rampa cinza-escuro, propriedades enferrujadas, andaime
azul-metálico, telhado vermelho-escuro) fazem os três níveis serem
distinguíveis a olho, sem HUD nenhum.

### Rampas viraram micro-degraus, não uma caixa inclinada

A ideia original (uma única `BoxMesh` rotacionada de praça a rua, como a
`Rampa` de exemplo da arena genérica anterior) **não bakeia como navmesh
conectado** no Godot/recast em `cell_size = 0,2`: o bake produz só uma lasca
minúscula e desconectada da inclinação, e `NavigationServer3D.MapGetPath`
nunca atravessa dali — confirmado isolando o problema numa cena mínima à
parte (dois blocos planos + uma rampa inclinada entre eles) antes de tocar a
arena de verdade.

A correção: cada rampa virou 8 degraus baixos e planos (1 m de corrida, 0,125
m de subida cada — a mesma inclinação de ~7° de antes, só que em escada
fina), a técnica que o recast realmente conecta de forma confiável. Um riser
de 12,5 cm continua indistinguível de uma rampa lisa para quem joga, e não é
"degrau" no sentido que o ticket proíbe (quina visível que trava inimigo) —
é sobre isso, não sobre lisura matemática literal.

Uma segunda causa, encontrada só depois de trocar as rampas: a laje plana de
cada braço de rua cobria por cima o próprio vão dos degraus, mais alta (0,0
m) que eles (até -0,125 m) — e o recast sempre bakeia só a superfície mais
alta de cada coluna de voxel, então os degraus ficavam soterrados e o navmesh
nunca descia de verdade. Cada braço de rua virou 3 caixas (2 laterais + 1
fechando a ponta) em vez de 1, com um vão do tamanho exato da pegada da rampa
— a própria rampa tapa o vão.

As quatro rampas conectadas foram verificadas por `UrbanArenaProbe`, uma a
uma, com `NavigationServer3D.MapGetPath` direto (não só visualmente).

### Objetos escaláveis, andaime, saliência e telhado

Contêiner, ônibus e caçamba (todos alcançáveis por pulo simples, `JumpHeight
= 2,2 m`) levam a um andaime; do andaime, uma saliência tipo marquise leva ao
telhado. Rua→ônibus, rua→contêiner, rua→caçamba e ônibus→andaime são cada um
um `NavigationLink3D` bidirecional próprio, testado individualmente pelo
probe. **Não há** ligação de navegação do andaime até a saliência de
propósito: diferente da spec 17 §5 (que só sanciona "rua → caçamba, caçamba →
andaime" para inimigos), o andaime→saliência chegou a existir numa primeira
versão, mas foi removido no code review — deixaria um inimigo literalmente
encostado na borda do telhado, esvaziando o "refúgio real do jogador" mesmo
sem pisar nele. O telhado fica **fora** da `NavigationRegion3D` de propósito
(nó irmão, não filho) — o bake nunca vê sua geometria, então a exclusão da
navegação inimiga é genuína, não um acidente de layout, e o probe confirma
isso pedindo um caminho até um ponto nele e checando que o `NavigationServer3D`
só alcança bem mais longe que a tolerância.

A rota "longa" (contêiner/ônibus → andaime → saliência → telhado) usa só
pulos simples — o dobro de pulo (`MaxAirJumps`) ainda não existe em NENHUM
personagem jogável nesta base de código (só o Berserker do ticket 20 vai
ganhar, e esse ticket não foi implementado ainda). A checklist pede "pulo
duplo OU rota longa"; a rota longa sozinha já satisfaz, e é a única
efetivamente jogável hoje.

### Fade de oclusão da câmera

`src/Camera/CameraOcclusionFader.cs` (novo) roda em `_PhysicsProcess` (não em
`_Process` — headless não roda `_Process` de forma confiável, e as sondas
deste projeto dependem de rodar headless), lança um raio câmera→jogador
contra a camada `PhysicsLayers.World`, e troca o material do que acertar por
um `StandardMaterial3D` com `TransparencyEnum.AlphaHash` (estipulagem/dither
de graça, sem shader próprio) na cor original com alfa reduzido. A câmera
nunca se move para desviar — só o objeto desaparece, preservando o pilar de
enquadramento fixo (spec 02 §4).

`combat_camera.tres` continua -45° (default da spec 02) para as arenas de
debug já existentes; a arena urbana ganhou seu próprio recurso,
`data/camera/urban_arena_camera.tres`, com `PitchDegrees = -55°` — o ângulo
que a spec 17 §4 já assume ao descrever as regras de restrição da câmera —
sem alterar o comportamento de nenhuma outra cena que compartilhava o
recurso antigo.

### O que não deu para verificar rigorosamente

"40 inimigos mantêm 60 fps" (linguagem da spec, não uma checkbox deste
ticket) não é algo que este ambiente headless consegue medir — não há
profiler de verdade rodando aqui, só a garantia estrutural de que o
orçamento de blocos/triângulos/draw calls/materiais está dentro do limite
(ver abaixo).

"Nada no quadrante nordeste bloqueia a visão" (spec 17 §4.2/§8) — a
`CameraMath.OffsetFromAngles(pitch=-55°, yaw=45°, distância=17)` desta arena
resolve, verificado tanto por cálculo quanto por leitura direta de
`CombatCamera.GlobalPosition` em runtime, num deslocamento de câmera em
**+X, +Z** relativo ao jogador (a review de código desta ticket pegou uma
primeira versão desta seção que descrevia isso como "sudeste", invertido).
Os props altos (andaime, saliência, telhado, ônibus, contêiner) ficam todos
em +X, **-Z** — quadrante vizinho ao da câmera, não o mesmo, então nenhum
fica na mesma esquina de onde a câmera realmente enquadra. Não há, porém, um
probe automatizado confirmando isso por projeção de câmera (seria um teste
bem mais caro para o ganho desta fase de bloqueio) nem uma verificação de
que "+X,+Z" é de fato o que a spec chama de "nordeste" nesta base de código
— os nomes `RuaNorte`/`RuaSul`/etc. desta arena foram escolhidos livremente
ao construí-la e não estão necessariamente alinhados com a bússola que quem
escreveu a spec tinha em mente. O que está verificado é a relação
geométrica (props fora da esquina da câmera), não o rótulo cardeal.

A spec 17 §2 fala em "área jogável ~70 × 70 m"; a pegada real construída é
60×60 m no total, e o interior efetivamente andável (entre as faces internas
dos quatro prédios) fica mais perto de 32×35 m. É um valor bem abaixo do
alvo da spec. Dado que este é explicitamente o passo de **bloqueio** ("Bloqueio
com primitivas agora; assets modulares só no milestone de arte" — spec 17
§7), redimensionar tudo agora — recalculando toda a geometria de rampas,
ligações de navegação e posições de prop já verificadas — pareceu um risco
maior que o ganho nesta fase; fica registrado aqui para uma futura
iteração de escala em vez de silenciado.

### Orçamento (spec 17 §7)

| Item | Alvo | Medido |
|---|---|---|
| Blocos modulares distintos | ≤ 25 | 20 (`BoxMesh` únicos; os 8 degraus por rampa reaproveitam só 2 desses 20) |
| Materiais únicos | ≤ 12 | 8 |
| Triângulos do cenário | ≤ 180 k | ~700 (58 `MeshInstance3D`, todos caixas de 12 tris) |
| Draw calls com a arena cheia | ≤ 900 | ~60 (um por `MeshInstance3D` da própria arena; folga enorme para inimigos/VFX) |

### Fallout na regressão

A troca completa da geometria da arena (usada por ~15 sondas pré-existentes
que assumem posições fixas) quebrou várias sondas cujas coordenadas
hardcoded caíam agora em lugares diferentes — corrigido uma a uma, mesmo
padrão iterativo dos tickets 25/26:

- `MovementProbe`: o teste de dash partia de um ponto agora dentro da faixa
  da rampa sul; movido para um trecho de rua plano e livre.
- `CombatProbe`/`CriticalProbe`: os manequins de treino (`Manequim2`/`3`)
  ficaram perto demais do spawn do jogador na nova arena e de um manequim
  reposicionado pelo próprio probe a cada golpe — um cone largo o bastante
  acertava os dois ao mesmo tempo. Afastados para longe do spawn.
- `CriticalProbe`/`AerialCombatProbe`: a margem de quadros antes do primeiro
  golpe (para o `MovementComponent.IsGrounded`, cacheado com 1 quadro de
  atraso por design, se acomodar) não bastava mais — a arena nova tem bem
  mais colisores estáticos para o servidor de física assentar no boot.
  Dobrada de 5 para 10 quadros nos dois probes.
- `AerialCombatProbe`: os testes de combate aéreo/mergulho usavam a origem
  do mundo como "chão plano" — que agora é o centro da praça rebaixada
  (-1,0 m, não 0,0 m). Realocados para um trecho de rua plano de verdade.
- `EnemyBrainProbe`: o teste de "não percebe através de parede" dependia de
  um `Obstaculo1` da arena genérica antiga que não existe mais; recriado
  usando a `VielaParede` (ticket 21) como oclusor. Separadamente, o teste de
  perseguição descobriu um bug de verdade (não só de coordenada): um grunt
  que NASCE dentro da praça rebaixada nunca conseguia sair perseguindo (ver
  abaixo).
- `EnemyPoolProbe`: a constante `Longe` (usada para reaquisição isolada de
  inimigos do pool) caía dentro do prédio da quina sudeste da arena nova; um
  grunt reaquirido lá nunca assentava no chão. Realocada para um trecho de
  rua plano.

### Bug real encontrado via regressão: `NavigationAgent3D` preso ao nascer numa área com desnível de bake

`EnemyBrainProbe` (fase de perseguição) expôs que um inimigo que nasce
dentro da praça rebaixada nunca começa a perseguir: `GetNextPathPosition()`
fica devolvendo indefinidamente o PRÓPRIO ponto de partida do caminho, cuja
altura bakeada (~-0,5 m, arredondada pelo `cell_height` do recast) difere da
altura real de colisão onde o personagem assenta (-1,0 m) por mais que o
`path_desired_distance` do `NavigationAgent3D` (0,4 m, o padrão). O agente
nunca considera ter "chegado" ao próprio primeiro ponto do caminho, e por
isso nunca avança para o segundo.

Confirmado comparando: um grunt que nasce fora da praça (chão sem desnível
de bake relevante) persegue normalmente; o mesmo grunt nascendo dentro da
praça trava, mesmo com um caminho válido e completo já calculado
internamente (`NavigationAgent3D.GetCurrentNavigationPath()` retorna os
pontos certos; só o avanço ao longo dele nunca acontece).

Corrigido subindo `path_desired_distance` de 0,4 para 1,0 m em
`scenes/characters/EnemyGrunt.tscn` — folga confortável acima do pior
desnível de bake observado na arena de três níveis, sem afetar a distância
de PARADA de verdade (`target_desired_distance`, ainda 0,4 m, que o
`NavigationMotor.StoppingDistance` também controla). Isto é uma correção de
jogo de verdade, não só um ajuste de sonda: qualquer inimigo desta arena que
nasça ou seja nocauteado para dentro da praça (o `Spawn7` do ticket 21 é
justamente ali, de propósito) dependia deste comportamento para conseguir
voltar a perseguir.

### Code review

Duas sub-agents em paralelo (Standards e Spec) revisaram o diff staged
contra `docs/plans/convencoes-de-codigo.md`/o baseline de smells e contra
este ticket + `docs/specs/17-arena-urbana.md`. Achados reais, corrigidos:

- **`LigacaoAndaimeSaliencia` removida.** A spec 17 §5 só sanciona "rua →
  caçamba, caçamba → andaime" para inimigos; a ligação extra do andaime até
  a saliência deixaria um inimigo encostado na borda do telhado, contra o
  próprio texto da spec ("refúgio real do jogador, alcançável só por ele").
  Removida de `Arena.tscn`; as quatro ligações restantes continuam testadas
  individualmente pelo `UrbanArenaProbe`.
- **Seção "nada alto no quadrante da câmera" corrigida.** A versão anterior
  desta mesma seção dizia "nenhum prop alto no quadrante sudeste" — invertido
  em relação ao que o cálculo de `CameraMath.OffsetFromAngles` realmente dá
  (câmera em +X,+Z relativo ao jogador). Reescrita para descrever a relação
  geométrica verificada (props no quadrante +X,-Z, vizinho ao da câmera, não
  o mesmo) em vez de um rótulo cardeal que a revisão mostrou estar errado.
- **`AerialCombatProbe`: coordenadas sem comentário viraram uma constante
  nomeada** (`AreaAberta`), com o mesmo padrão de comentário que
  `EnemyBrainProbe`/`EnemyPoolProbe`/`MovementProbe` já usavam para suas
  próprias realocações.
- **Disclosure de área jogável adicionado**: a spec pede ~70×70 m, a pegada
  construída é 60×60 m (interior andável mais perto de 32×35 m) — registrado
  como um gap conhecido do bloqueio, não redimensionado agora (risco de
  recalcular toda a geometria já verificada por um ganho pequeno nesta fase).

Não alterado, por ser um padrão já estabelecido no projeto (não introduzido
por este ticket): a alocação de `PhysicsRayQueryParameters3D.Create(...)` a
cada `_PhysicsProcess` em `CameraOcclusionFader`, apontada pela Standards
review como tecnicamente contra convenções §5 ("proibido `new` em
`_Process`/`_PhysicsProcess`") — mas é o MESMO idiomatismo que
`Perception.LinhaDeVisaoLivre` e `HitscanWeapon` já usam (um raio por
quadro), citado nos próprios comentários da classe. Mudar só aqui deixaria
o código deste ticket inconsistente com o resto da base; ficaria para uma
limpeza maior de todos os três pontos de uma vez, fora do escopo aqui.

Depois das correções, os probes afetados (`AerialCombatProbe`,
`UrbanArenaProbe`, `EnemyBrainProbe`, `EnemyVisibilityProbe`,
`AerialEnemyProbe`) foram re-executados e continuam passando.
