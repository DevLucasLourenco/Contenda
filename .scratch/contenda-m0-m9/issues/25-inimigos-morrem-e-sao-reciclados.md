# 25: Inimigos morrem, voltam ao pool e reaparecem íntegros

**What to build:** a base para dezenas de inimigos por onda sem engasgo. Ao
morrer, o inimigo toca a animação, some e volta para um estoque; ao ser
reaproveitado, volta **exatamente** como se tivesse acabado de nascer.

**Blocked by:** 22

**Status:** ready-for-agent

- [ ] Morrer toca animação de morte e desliga a colisão antes de sumir
- [ ] O inimigo morto volta para o estoque em vez de ser destruído
- [ ] Reaproveitar um inimigo devolve vida cheia, sem atordoamento e sem
      modificadores de qualquer fonte
- [ ] Nenhum aviso de evento fica pendurado de uma vida anterior
- [ ] Nenhuma área de dano fica ativa órfã de um golpe interrompido pela morte
- [ ] Reciclar o mesmo inimigo **cem vezes** deixa o estado idêntico ao do recém-criado
- [ ] Uma onda inteira nascendo de uma vez não produz engasgo visível
- [ ] O estoque é preparado antes da partida começar, não durante

## Comments

Vazamento de aviso de evento em objeto reciclado é o bug que este desenho mais
convida, e o mais difícil de achar depois: ele se manifesta como dano dobrado ou
morte instantânea vinte minutos depois, sem relação aparente com a causa.

O teste de cem ciclos não é exagero — é a única forma de pegar isso antes de
virar relato de jogador.
