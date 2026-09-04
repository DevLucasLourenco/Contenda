# 30: O jogo abre num menu e o menu leva à partida

**What to build:** o jogo deixa de começar direto na arena. Abre num menu com a
cidade ao fundo, o jogador escolhe iniciar, vê os modos disponíveis, e entra na
partida sem a tela congelar durante o carregamento.

**Blocked by:** 28

**Status:** ready-for-agent

- [ ] O menu principal oferece iniciar, configurações e sair
- [ ] O fundo é a própria cidade em 3D, vista pela câmera do jogo
- [ ] Escolher iniciar mostra os modos; só o modo horda é jogável
- [ ] Os modos futuros aparecem visíveis e desabilitados, marcados como em breve
- [ ] Trocar de tela não congela o jogo: há uma tela de carregamento com
      progresso real
- [ ] Dá para navegar tudo por teclado e por mouse
- [ ] Voltar funciona em todas as telas
- [ ] **Sair de uma partida para o menu não deixa o menu pausado**
- [ ] Navegar vinte vezes entre as telas não acumula nós esquecidos

## Comments

Mostrar os modos futuros desabilitados custa nada e comunica a ambição do
projeto — o jogador entende que horda é o começo, não o produto inteiro.

O critério do menu pausado parece bobo e é o bug clássico desta transição:
sair do pause para o menu leva junto o estado de pausa e a tela nasce morta.
