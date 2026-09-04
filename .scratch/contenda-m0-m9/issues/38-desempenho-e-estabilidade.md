# 38: Quarenta inimigos a 60 quadros, e meia hora sem cair

**What to build:** a garantia de que o jogo aguenta. O pior caso previsto roda
liso, e uma sessão longa não degrada nem trava.

**Blocked by:** 37

**Status:** ready-for-agent

- [ ] Quarenta inimigos na cidade com arte e efeitos: **60 quadros por segundo**
      na máquina de referência
- [ ] O pior 1% dos quadros também cabe no orçamento — média boa com engasgos não conta
- [ ] Existe um cenário de medição repetível, comparável entre commits
- [ ] Trinta minutos de jogo contínuo sem queda e sem degradar
- [ ] Quinhentos inimigos reciclados ao longo da sessão sem vazamento
- [ ] Cinquenta trocas de tela sem acumular nós esquecidos
- [ ] Nada é alocado a cada quadro no caminho crítico
- [ ] Testado também em máquina mais fraca que a de referência
- [ ] Alt-tab, troca de resolução e troca de monitor durante a partida não quebram
- [ ] Arquivos de configuração e de perfil corrompidos não impedem o jogo de abrir

## Comments

Medir só a média esconde o problema que o jogador sente: o engasgo no
nascimento de onda. É o pior 1% que estraga a experiência.

Se os 60 quadros não vierem, reduzir o teto de inimigos simultâneos e ajustar o
desenho das ondas é melhor decisão que aceitar 40 quadros. Fluidez vale mais que
quantidade.
