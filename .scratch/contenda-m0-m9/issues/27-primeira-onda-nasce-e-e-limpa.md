# 27: Uma onda de inimigos nasce, é limpa e dá lugar à próxima

**What to build:** o laço central do modo horda. Inimigos aparecem aos poucos
em pontos afastados, o jogador limpa, respira um instante, e a onda seguinte
começa — com um anúncio na tela.

**Blocked by:** 25, 23

**Status:** ready-for-agent

- [ ] Os inimigos de uma onda aparecem espaçados no tempo, não todos de uma vez
- [ ] **Nenhum inimigo nasce dentro do campo de visão imediato do jogador**
- [ ] Nenhum nasce colado nele
- [ ] Um marcador no chão avisa antes de o inimigo surgir
- [ ] O recém-nascido fica brevemente invulnerável, para não morrer sem ser visto
- [ ] Matar todos avança para a próxima onda, com um intervalo de respiro
- [ ] O anúncio da onda aparece e some sozinho
- [ ] A composição e o ritmo da onda vêm de arquivo de dados, não do código
- [ ] **Um inimigo preso no cenário não trava a partida:** se não houver mais
      ninguém para matar por tempo demais, a onda avança assim mesmo

## Comments

O último critério é rede de segurança, não preguiça. Um inimigo perdido atrás de
um carro é a diferença entre uma partida de dez minutos e uma partida que nunca
termina — e vai acontecer.

Contar inimigos varrendo a cena a cada quadro é caro e frágil. A contagem vem do
evento de abate.
