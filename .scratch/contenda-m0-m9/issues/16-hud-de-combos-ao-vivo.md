# 16: A interface ensina as combinações e mostra o que o jogo entendeu

**What to build:** uma lista permanente das habilidades com suas sequências, que
**reage enquanto o jogador digita** — acendendo o que já foi digitado, apagando
o que não casa mais, e destacando a linha pronta para confirmar.

**Blocked by:** 14, 12

**Status:** ready-for-agent

- [ ] A lista aparece sozinha a partir dos dados do personagem; trocar de
      personagem troca a lista inteira
- [ ] Digitar o primeiro símbolo acende a parte correspondente e apaga as linhas
      impossíveis
- [ ] Completar uma sequência destaca a linha e sinaliza que basta confirmar
- [ ] Habilidade em recarga aparece esmaecida com o tempo restante — mas
      **continua listada**, porque o jogador precisa decorar a sequência
- [ ] Habilidade sem mana mostra o custo em vermelho
- [ ] Confirmar sem casamento faz a lista piscar
- [ ] Um jogador que nunca viu o jogo executa uma habilidade **de propósito** em
      menos de dois minutos, sem explicação externa

## Comments

Este é o ticket que decide se o sistema de comandos é aprendível. Sem ele, a
mecânica parece quebrada: o jogador digita e nada acontece, sem saber por quê.

O último critério é o único que importa de verdade. Teste com alguém de fora.
