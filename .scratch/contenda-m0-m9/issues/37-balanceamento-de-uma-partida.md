# 37: Uma partida completa é difícil na medida certa

**What to build:** o ajuste dos números até a partida ter a curva certa. Onda 1
confortável, onda 3 tensa, onda 4 com quase-morte, onda 5 exigindo
transformação — e os dois personagens igualmente viáveis.

**Blocked by:** 35, 36

**Status:** ready-for-human — registrar dez partidas completas por personagem antes de ajustar os valores finais

- [ ] Dez partidas completas com cada personagem, registradas por escrito
- [ ] A curva de dificuldade bate com a intenção acima
- [ ] **Vencer a onda 5 sem transformar é inviável**
- [ ] Os dois personagens chegam ao fim com esforço comparável
- [ ] **Todas as quatro habilidades de cada um são usadas** — habilidade que
      ninguém usa é habilidade mal balanceada, não habilidade opcional
- [ ] Transformar-se é uma decisão, não um hábito: quem fica transformado o tempo
      todo indica dreno barato demais
- [ ] O crítico é sentido sem ser dominante
- [ ] Pulo, dash e combate aéreo são usados de propósito, não por acidente
- [ ] Os números finais são atualizados nas tabelas das specs, no mesmo commit

## Comments

Balanceamento é poço sem fundo. Timebox de três dias: publica-se a 0.1 e ajusta-se
com jogador real, que descobre em uma tarde o que ninguém vê sozinho em uma
semana.

O critério das quatro habilidades é o mais revelador. Se uma nunca sai, ou o
custo está caro, ou a sequência é desconfortável, ou o efeito é fraco — e cada
causa tem remédio diferente.

Os tickets 35 e 36 estão liberados. A auditoria dos dados confirmou que as
formas duram aproximadamente 20 s (Berserker: 80/4) e 19 s (Overdrive:
95/5): `ManaComponent.Drain` reinicia o atraso de regeneração em cada quadro,
portanto a mana não regenera enquanto a forma está ativa. A spec 16 também
removeu explicitamente o antigo bônus de cadência do Overdrive ao trocar para o
braço-canhão. Não altere esses valores sem resultados de partidas; use a ficha
em [`docs/playtests/37-balanceamento.md`](../../../docs/playtests/37-balanceamento.md).

**Observação de playtest em 2026-10-03:** o usuário relatou que o espadachim
está funcionando bem, mas a pistoleira não mirava e o M1 normal não disparava.
As correções foram feitas e a validação foi aprovada pelo usuário. Este relato
não registra dez partidas completas por personagem nem resultados numéricos;
os critérios de balanceamento permanecem abertos até esses dados serem
registrados na ficha de playtest.
