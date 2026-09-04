# 18: Golpes críticos, inconfundíveis quando acontecem

**What to build:** parte dos golpes passa a sair muito mais forte, com sinal
claro. O espadachim critica com mais frequência que a pistoleira, e a diferença
precisa ser sentida sem olhar número nenhum.

**Blocked by:** 07, 11

**Status:** ready-for-agent

- [ ] Existem dois atributos novos: chance de crítico e multiplicador de crítico
- [ ] O espadachim critica com o dobro da frequência da pistoleira
- [ ] Inimigos não criticam
- [ ] Um ataque em área é crítico **em todos os alvos ou em nenhum** — nunca em
      alguns
- [ ] O número de dano do crítico é maior, de outra cor e com brilho
- [ ] O congelamento do golpe crítico é mais longo que o do golpe normal
- [ ] O som do crítico tem uma camada a mais
- [ ] Os valores de chance e multiplicador vêm de arquivo de dados

## Comments

Sortear por alvo em vez de por golpe transformaria ataques de área em loteria e
tornaria o crítico ilegível — o jogador veria cinco números diferentes sem
entender por quê. Um sorteio por golpe.

Este ticket é pré-requisito do Berserker: a forma dá +35 pontos percentuais de
chance, e sem o sistema de crítico ela não teria o que multiplicar.
