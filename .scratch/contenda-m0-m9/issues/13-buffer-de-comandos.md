# 13: O jogo registra sequências de WASD sem atrapalhar o movimento

**What to build:** andar segurando uma tecla continua andando; **tocar** a tecla
grava um símbolo numa fila curta que expira sozinha. É a base da mecânica
assinatura do jogo, e precisa conviver com o movimento sem que um estrague o outro.

**Blocked by:** 05, 03

**Status:** ready-for-agent

- [ ] Andar segurando W por três segundos grava **um** símbolo, não sessenta
- [ ] Tocar W duas vezes rápido grava dois símbolos e o personagem mal sai do lugar
- [ ] Um símbolo isolado some sozinho depois de menos de um segundo
- [ ] A sequência inteira é descartada se o jogador parar no meio
- [ ] Morrer, tomar atordoamento ou pausar limpa a fila
- [ ] Existe uma consulta que responde "qual habilidade casa com esta sequência?"
      e outra que responde "quais ainda podem casar se ele continuar digitando?"
- [ ] Duas habilidades declarando a mesma sequência **quebram o carregamento**
      com mensagem clara, em vez de uma delas nunca disparar
- [ ] Testes cobrindo expiração, capacidade, casamento exato, prefixo e ambiguidade

## Comments

Este é o ticket de maior risco do projeto (R1 e R4). Se em teste as habilidades
saírem sem intenção durante o reposicionamento, o remédio é exigir intervalo
mínimo entre símbolos iguais — não encurtar a janela, que piora a execução
proposital.
