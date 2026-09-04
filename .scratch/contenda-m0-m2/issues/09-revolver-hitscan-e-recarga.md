# 09: Ataque básico com revólver, munição e recarga

**What to build:** trocando para a pistoleira, o **mesmo** botão esquerdo passa
a atirar em vez de golpear. O tiro acerta onde o cursor aponta, gasta munição do
tambor e recarrega sozinho quando esvazia. Segurar o botão mantém a cadência.

**Blocked by:** 08

**Status:** ready-for-agent

- [ ] Trocar de personagem muda o que o botão esquerdo faz
- [ ] O tiro acerta o ponto mirado, com dispersão pequena e rastro visível
- [ ] A munição diminui e a recarga acontece ao esvaziar
- [ ] Segurar o botão dispara em cadência constante
- [ ] Aumentar o atributo de cadência acelera o disparo **sem** alterar os dados
      da arma
- [ ] A troca de personagem não introduz nenhuma ramificação sobre o nome do
      personagem no código

## Comments

Este é o ticket que **prova a arquitetura data-driven**. Se ele exigir um
`if (personagem == …)` em qualquer lugar, a abstração de arma do ticket 08 está
errada e é aqui que se descobre — não no M7, com sete sistemas em cima.

O critério da cadência existe por antecipação: a transformação Overdrive, no M4,
aumenta a cadência da pistoleira em 50 %. Se isso exigir editar os dados da
arma, o bloco de atributos do ticket 07 não está sendo lido no lugar certo.

Sem queda de projétil: tiro instantâneo. O único projétil real do jogo é uma
habilidade da pistoleira, no M3.
