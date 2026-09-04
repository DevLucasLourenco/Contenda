# 28: Cinco ondas, um chefe, e a partida termina

**What to build:** a partida completa. Dificuldade crescente ao longo de cinco
ondas, um chefe na última, e um fim de verdade — vitória se ele cair, derrota se
o jogador cair antes.

**Blocked by:** 27, 24

**Status:** ready-for-agent

- [ ] As cinco ondas rodam do início ao fim sem travar
- [ ] A composição varia, não só a quantidade: aparecem tipos que **mudam** o
      comportamento do jogador, não apenas mais do mesmo
- [ ] Um elite aparece antes do chefe e é sentido como salto de dificuldade
- [ ] O chefe tem mais de um golpe e reforços chegando, em vez de só muita vida
- [ ] A praça funciona como funil na onda do elite e na do chefe
- [ ] Morrer encerra a partida em derrota; limpar a última onda, em vitória
- [ ] Trocar o arquivo do conjunto de ondas muda toda a progressão sem recompilar
- [ ] Uma partida completa leva entre oito e doze minutos

## Comments

Chefe que é só uma esponja de vida cansa em trinta segundos. Ele reusa a mesma
máquina de estados dos outros inimigos, com golpes alternados e parâmetros
diferentes — nenhuma classe nova.

A onda 5 deve ser inviável sem transformação. Se der para vencer sem se
transformar, o custo de mana está barato demais ou o chefe está fraco.
