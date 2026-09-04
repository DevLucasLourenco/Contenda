# 12: HUD de vida e mana

**What to build:** o jogador passa a enxergar seu próprio estado sem olhar o
console. Barra de vida com camada de dano atrasada, barra de mana com número, e
os dois reagindo em tempo real.

**Blocked by:** 09, 10.

**Status:** ready-for-agent

- [ ] `HealthBar` com **camada de dano atrasada**: a fatia perdida esvazia em
      0.4 s depois do golpe, comunicando quanto se levou
- [ ] `ManaBar` refletindo consumo e regeneração, com o delay visível
- [ ] valores numéricos ao lado das barras — sem número, o jogador não aprende os
      custos
- [ ] `HudController` ligado ao `CharacterController` do jogador **uma vez**,
      distribuindo as referências; nenhum widget procura o jogador por conta
      própria
- [ ] `CanvasLayer` + `Control` apenas — a única parte do jogo onde 2D é
      permitido
- [ ] trocar de personagem por tecla de debug atualiza as duas barras

## Comments

Sem estilização: o HUD definitivo, com tema, guia de combos e seletor de
transformação, é o M7. Aqui basta ser legível.

Este é o ticket mais fino do conjunto e o primeiro candidato a mesclar, caso o
fatiamento se mostre granular demais na prática — o conteúdo caberia distribuído
entre o 10 e o 11.
