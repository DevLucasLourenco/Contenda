# 38: Quarenta inimigos a 60 quadros, e meia hora sem cair

**What to build:** a garantia de que o jogo aguenta. O pior caso previsto roda
liso, e uma sessão longa não degrada nem trava.

**Blocked by:** 37 (implementação iniciada com dispensa explícita do registro de dez partidas; a validação de balanceamento do 37 continua em aberto)

**Status:** ready-for-human — a medição disponível foi headless; falta validar critérios que dependem de renderização gráfica e interação manual

- [ ] Quarenta inimigos na cidade com arte e efeitos: **60 quadros por segundo**
      na máquina de referência
- [ ] O pior 1% dos quadros também cabe no orçamento — média boa com engasgos não conta
- [x] Existe um cenário de medição repetível, comparável entre commits (`--bench=40enemies`, duração e revisão configuráveis, CSV em `user://`)
- [ ] Trinta minutos de jogo contínuo sem queda e sem degradar
- [x] Quinhentos inimigos reciclados ao longo da sessão sem vazamento (500 ciclos reais de `Acquire/Release`, estoque cheio e limpeza passaram sem crescimento de nós nem avisos no encerramento da sonda)
- [x] Pelo menos cinquenta trocas de tela sem acumular nós esquecidos (79 confirmadas)
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

**Execução em 2026-10-03:** o usuário autorizou iniciar este ticket sem aguardar
os dez registros do ticket 37. A sonda `EnemyPoolProbe` passou em 500 ciclos
reais de aquisição/liberação; `MenuProbe` passou com 79 transições confirmadas
e contagem de nós estável após o aquecimento. `dotnet test Contenda.sln
-c ExportRelease --no-restore` passou (442/442); builds Debug e `ExportRelease`
passaram sem warnings ou erros. As sondas de catálogo de habilidades e de
revólver também passaram (o probe de revólver registrou 18 acertos).

O benchmark foi executado por 60 s duas vezes com 40 inimigos posicionados
dentro do raio de detecção e VFX, mas em modo headless. Os resultados variaram:
8.270–8.569 quadros, p50 6,875–6,875 ms, p99 15,525–17,875 ms, máximo
113,900–166,332 ms e 0,63–1,29% acima de 16,6 ms; física média 12,100–12,432
ms; 1.609.128–1.614.240 bytes gerenciados em 1.783–1.814 intervalos; nós
estáveis em 2.974; memória estática 85,858–92,233 MiB. Resultado gráfico:
`NOT_RUN_HEADLESS`; GPU e tamanho de janela não estavam disponíveis. Portanto,
esses tempos não validam os critérios de 60 fps/p99 na máquina de referência.
O contador de alocações também não chegou a zero. A sonda atribuiu 2.120 bytes
à instrumentação e 0 bytes ao disparo dos VFX na execução curta de 5 s.

Um encerramento de benchmark não verbose reportou 14 instâncias `ObjectDB` e
6 recursos ainda vivos; a repetição verbose após a limpeza das vozes de áudio
encerrou sem esses avisos. A execução final da `EnemyPoolProbe`, depois da
limpeza dos streams e da rajada do pool, concluiu 500 ciclos, verificou o
estoque cheio, devolveu todos os inimigos e encerrou sem avisos. Assim, o
critério dos 500 ciclos está aprovado; a sessão longa de 30 min continua
pendente para validar estabilidade prolongada.

Continuam pendentes: sessão de 30 min, execução gráfica/profiler em hardware de
referência e mais fraco, verificação manual de alt-tab/resolução/monitor, teste
com arquivos de configuração corrompidos e eliminação/confirmação das
alocações gerenciadas restantes no caminho crítico. O usuário dispensou apenas
a espera dos registros do ticket 37 para iniciar o trabalho; isso não registra
nem aprova aqueles playtests.
