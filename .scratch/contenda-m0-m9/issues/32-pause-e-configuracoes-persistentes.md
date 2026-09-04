# 32: O jogo pausa, e as configurações sobrevivem a fechar o jogo

**What to build:** apertar Esc no meio da luta congela tudo e abre um menu. Dali
dá para ajustar vídeo, áudio, teclas e opções de jogo — e essas escolhas
continuam valendo na próxima vez que o jogo abrir.

**Blocked by:** 30

**Status:** ready-for-agent

- [ ] Esc congela a partida e abre continuar, configurações, reiniciar e sair
- [ ] O HUD continua visível atrás do menu — o jogador pausa justamente para ler
      as combinações
- [ ] Vídeo: modo de janela, resolução, sincronização, limite de quadros, sombras
      e escala de renderização
- [ ] Áudio: volumes separados, todos audivelmente diferentes ao mexer
- [ ] Teclas: **todas** as ações remapeáveis, com aviso de conflito e opção de
      restaurar padrões
- [ ] Jogo: intensidade do tremor de tela, números de dano, guia de combos e a
      **tolerância da janela de comandos**
- [ ] Fechar e reabrir o jogo preserva tudo
- [ ] Apagar o arquivo de configurações faz o jogo abrir com os padrões, sem quebrar
- [ ] As configurações são as mesmas pelo menu principal e pelo pause

## Comments

A tolerância da janela de comandos como opção é acessibilidade deliberada: o
sistema de sequências é a barreira de entrada do jogo, e a tolerância de tempo
varia muito entre jogadores. Quem não consegue executar não joga.

Remapear a tecla de confirmar precisa refletir na guia de combos, senão o HUD
passa a mentir.
