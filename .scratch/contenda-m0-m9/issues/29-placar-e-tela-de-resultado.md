# 29: O placar recompensa agressividade e a partida tem desfecho

**What to build:** um motivo para rejogar. Abater rápido e sem apanhar vale mais
pontos, e ao fim da partida uma tela mostra o que aconteceu e oferece o próximo
passo.

**Blocked by:** 28

**Status:** ready-for-agent

- [ ] Cada abate soma pontos, e ondas mais avançadas valem mais
- [ ] Abates em sequência rápida multiplicam o ganho
- [ ] **Apanhar zera o multiplicador** — o placar premia quem arrisca e acerta
- [ ] Limpar uma onda sem apanhar dá bônus
- [ ] O placar aparece durante a partida e reage na hora
- [ ] A tela final mostra pontos, ondas, abates, tempo e personagem
- [ ] Dela dá para tentar de novo, trocar de personagem ou voltar ao menu
- [ ] O melhor resultado de cada personagem é guardado entre sessões
- [ ] A gravação do recorde não corrompe o arquivo se o jogo fechar no meio

## Comments

Zerar o multiplicador ao apanhar é o que impede a estratégia de ficar num canto
atirando. Sem isso o placar recompensa o oposto do que o jogo quer ensinar.

Gravação atômica não é preciosismo: o jogo fecha durante uma tela de resultado
mais vezes do que parece, e um perfil corrompido apaga todo o histórico.
