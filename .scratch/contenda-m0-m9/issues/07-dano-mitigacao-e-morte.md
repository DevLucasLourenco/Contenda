# 07: Manequim leva dano com mitigação e morre uma vez só

**What to build:** existe um manequim de treino na arena. Aplicar dano nele
reduz a vida já descontada da defesa, mostra que ele apanhou, e ao chegar a zero
ele morre — uma vez só, mesmo levando dois golpes no mesmo instante. É a
primeira vez que o jogo tem consequência.

**Blocked by:** 05

**Status:** ready-for-agent

- [ ] O manequim tem vida visível e apanha
- [ ] O dano recebido é reduzido pela defesa do alvo
- [ ] Dois golpes no mesmo frame disparam morte **uma** vez
- [ ] Um alvo recém-atingido fica brevemente invulnerável
- [ ] Adicionar e depois remover um modificador devolve o atributo ao valor base
      **exato**
- [ ] Mudar a vida máxima não mata nem cura de graça
- [ ] Existe um jeito de devolver o alvo ao estado de recém-criado
- [ ] Testes cobrindo dano, mitigação, morte idempotente e reversão de modificador

## Comments

Este ticket entrega dois pilares que quase todo o resto do jogo usa.

**O bloco de atributos com modificadores por fonte.** Transformações, buffs e
equipamentos nunca escrevem em campos de outros componentes: escrevem
modificadores marcados com sua origem, e os componentes leem o valor final.
É isso que torna reverter uma transformação, no M4, uma única chamada em vez de
uma fonte permanente de bugs. Ver ADR-006.

**A fila de dano.** Dano recebido no frame é enfileirado e resolvido num ponto
único, senão dois golpes simultâneos disparam morte duas vezes. Está na lista de
bugs previsíveis da spec 15, §5.

O "jeito de devolver ao estado de recém-criado" parece prematuro aqui, mas é o
contrato de reciclagem de que o pool de inimigos depende no M5. Sai barato agora
e caro depois.
