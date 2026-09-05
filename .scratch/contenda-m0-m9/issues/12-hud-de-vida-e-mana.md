# 12: HUD de vida e mana

**What to build:** o jogador passa a enxergar seu próprio estado sem olhar o
console. Barra de vida com camada de dano atrasada, barra de mana com número, e
os dois reagindo em tempo real.

**Blocked by:** 09, 10.

**Status:** CONCLUÍDO

- [x] `HealthBar` com **camada de dano atrasada**: a fatia perdida esvazia em
      0.4 s depois do golpe, comunicando quanto se levou
- [x] `ManaBar` refletindo consumo e regeneração, com o delay visível
- [x] valores numéricos ao lado das barras — sem número, o jogador não aprende os
      custos
- [x] `HudController` ligado ao `CharacterController` do jogador **uma vez**,
      distribuindo as referências; nenhum widget procura o jogador por conta
      própria
- [x] `CanvasLayer` + `Control` apenas — a única parte do jogo onde 2D é
      permitido
- [x] trocar de personagem por tecla de debug atualiza as duas barras

## Comments

Sem estilização: o HUD definitivo, com tema, guia de combos e seletor de
transformação, é o M7. Aqui basta ser legível.

Este é o ticket mais fino do conjunto e o primeiro candidato a mesclar, caso o
fatiamento se mostre granular demais na prática — o conteúdo caberia distribuído
entre o 10 e o 11.

---

**Concluído em 2026-09-05.** Fecha o M2 junto com o 11 (que fica pendente à
parte — feel/VFX, fora do escopo deste ticket).

**O que foi construído:**

- [`DamageLayerState`](../../../src/UI/HUD/DamageLayerState.cs) — POCO, como o
  `MeleeCombo`: a fatia perdida esvazia LINEARMENTE até o valor atual, na
  duração configurada. A velocidade é fixada uma única vez, no instante do
  golpe (como o `LungeMotion` já faz para o avanço), não recalculada a cada
  quadro a partir do que resta — recalcular produz uma curva exponencial que
  se aproxima do alvo sem nunca alcançá-lo de verdade. Um teste com muitos
  quadros pequenos (não um só `Tick` grande) prova a diferença; é exatamente o
  tipo de bug que só aparece em produção, nunca num teste de uma chamada só.
- [`HealthBar`](../../../src/UI/HUD/HealthBar.cs) e
  [`ManaBar`](../../../src/UI/HUD/ManaBar.cs) — `Control` puro, sem `Container`
  de layout (que reescreveria o `Size` definido a cada quadro). `ManaBar` não
  tem camada de dano própria: o atraso da regeneração já é visível de graça,
  porque `Percent` simplesmente não muda enquanto a pausa do ticket 10 dura.
- [`HudController`](../../../src/UI/HUD/HudController.cs) distribui as
  referências uma vez; nenhuma barra procura o jogador. Encontra o jogador
  pelo grupo `"player"` (`Character.tscn`), não por `NodePath` — motivo
  abaixo.
- **Desvio da spec 01 §6, registrado aqui de propósito:** a spec desenha o HUD
  lendo estado em `_Process`. Na prática, `_Process` não roda de forma
  confiável em modo `--headless`, e as sondas deste projeto DEPENDEM de rodar
  headless — um HUD só verificável brincando no editor não teria sonda
  nenhuma. `HealthBar`, `ManaBar` e `HudController` leem e agem em
  `_PhysicsProcess`. Isso também corrigiu, de graça, uma leitura de
  `IsActionJustPressed` que estava em `_Process` na tecla de debug — exatamente
  o anti-padrão que as convenções §4 já avisavam ("engole entradas em
  framerate alto").
- **O HUD não vive dentro de `Arena.tscn`.** Instanciar uma cena de
  `scenes/ui/` dentro de uma cena de gameplay reprova o verificador anti-2D
  (`tools/check-no-2d.sh` trata isso como "cena de interface dentro do
  mundo" — caso 4 do autoteste dele). Em vez disso, `GameBootstrap` (autoload)
  carrega `Hud.tscn` e o adiciona à raiz da árvore com
  `CallDeferred(AddChild)` — direto falha com "Parent node is busy setting up
  children", porque a raiz ainda está registrando os outros autoloads durante
  o próprio `_Ready` do `GameBootstrap`. Isso também torna o HUD independente
  de qual nível está carregado, sem custo extra.
- `CharacterController.SwitchDefinition` reconfigura o mesmo personagem em
  runtime (reaplica só `Configure` em todos os componentes) — é a mesma
  operação que uma transformação fará no M4. A tecla de debug
  (`debug_switch_character`, F1) usa isso para alternar entre os
  `DebugArchetypes` do `Hud.tscn`, só em build de debug.

**Verificado por [`HudProbe`](../../../src/Tools/HudProbe.cs)**, headless, na
árvore real: as duas barras nascem cheias, um golpe direto na vida derruba o
preenchimento na hora e mantém a camada de dano atrás por um instante, a
camada alcança o preenchimento depois do tempo configurado, gastar mana move a
barra de mana, e a tecla de debug troca o `MaxHealth` efetivo (prova que
`SwitchDefinition` rodou) sem quebrar nenhuma das duas frações exibidas.

**Não verificado nesta sessão:** aparência real das barras (posição, cores,
proporção) — a CLI não renderiza a UI. As cores e posições em `Hud.tscn` são
um palpite razoável, não um layout aprovado; vale abrir no editor antes de
considerar isto visualmente pronto, mesmo sem estilização.
