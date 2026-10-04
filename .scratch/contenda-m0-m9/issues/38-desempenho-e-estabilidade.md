# 38: Quarenta inimigos a 60 quadros, e meia hora sem cair

**What to build:** a garantia de que o jogo aguenta. O pior caso previsto roda
liso, e uma sessão longa não degrada nem trava.

**Blocked by:** 37 (implementação iniciada com dispensa explícita do registro de dez partidas; a validação de balanceamento do 37 continua em aberto)

**Status:** concluído por decisão de escopo do usuário em 2026-10-04 — os limites de desempenho restantes foram aceitos e adiados para depois do foco atual de release; os resultados reprovados continuam registrados abaixo

- [ ] Quarenta inimigos na cidade com arte e efeitos: **60 quadros por segundo**
      na máquina de referência
- [ ] O pior 1% dos quadros também cabe no orçamento — média boa com engasgos não conta
- [x] Existe um cenário de medição repetível, comparável entre commits (`--bench=40enemies`, duração e revisão configuráveis, CSV em `user://`)
- [ ] Trinta minutos de jogo contínuo sem queda e sem degradar
- [x] Quinhentos inimigos reciclados ao longo da sessão sem vazamento (500 ciclos reais de `Acquire/Release`, estoque cheio e limpeza passaram sem crescimento de nós nem avisos no encerramento da sonda)
- [x] Pelo menos cinquenta trocas de tela sem acumular nós esquecidos (79 confirmadas)
- [ ] Nada é alocado a cada quadro no caminho crítico
- [x] Testado também em máquina mais fraca que a de referência (GPU local GeForce MX110; resultados gráficos documentados abaixo)
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
e contagem de nós estável após o aquecimento. Uma execução anterior de
`dotnet test Contenda.sln -c ExportRelease --no-restore` passou (442/442), mas
foi anterior à correção de `MultiMesh.VisibleInstanceCount`. Na tentativa final
após essa correção, o projeto compilou, mas o Windows App Control bloqueou o
carregamento da DLL de testes (`0x800711C7`), então a suíte desta versão não foi
confirmada. O build Debug passou sem warnings ou erros. As sondas de catálogo
de habilidades e de revólver também passaram (o probe de revólver registrou 18
acertos).

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

Uma execução gráfica de 60 s a 1920×1080 na máquina local (NVIDIA GeForce
MX110, inferior à GPU de referência GTX 1060/Vega 8) reprovou o orçamento:
805 quadros, p50 74,42 ms, p99 93,73 ms, máximo 107,75 ms e 100% dos quadros
acima de 16,6 ms; processo médio 57,80 ms, física 9,85 ms e navegação 0,41 ms.
Uma execução diagnóstica de 60 s sem o fade da câmera deu p50 31,28 ms e p99
49,78 ms, também com 100% acima do orçamento. A medição de oclusão encontrou
dois testes de raio e um colisor por tick, com busca de 42,4 μs em média.
A fachada `PredioSul` agrupava várias malhas num único colisor e o fade alterava
o material de oito superfícies. Uma versão intermediária que selecionava a
malha mais próxima reduziu o trabalho do fade, mas podia deixar visuais
atravessados pelo segmento sem fade. O fader agora seleciona todas as caixas de
visuais que cruzam o segmento câmera-alvo dentro dos colisores atingidos,
incluindo transforms individuais de `MultiMesh`. Para cada `MultiMesh`, um
proxy translúcido recebe somente as instâncias selecionadas e suas transformações;
as originais são restauradas ao sair da linha de visão. O `UrbanArenaProbe`
gráfico verificou várias instâncias no mesmo colisor, preservando a instância
fora do segmento, além de uma instância no segmento mas oculta por
`VisibleInstanceCount`. No benchmark gráfico final de 60 s com essa versão, o
fade atingiu duas superfícies: 1.993 quadros, p50 29,48 ms, p99 50,03 ms,
máximo 88,52 ms e 99,95% acima de 16,6 ms; processo médio 27,11 ms, física
10,34 ms e navegação 0,43 ms. Foram 3.603 ticks de oclusão, dois testes de raio
e um colisor por tick, com busca média/máxima de 28,3/154 μs. O fader alocou
0 bytes em 3.603 ticks; o processo inteiro alocou 2.171.688 bytes em 1.022 de
1.993 intervalos, com pico de 582.576 bytes. A contagem de nós ficou estável
em 2.992. O custo continua acima do orçamento, portanto 60 fps não foi
aprovado nesta máquina. A execução a 960×540 também ficou acima do orçamento
(p50 36,12 ms, p99 50,83 ms).

A execução final da `EnemyPoolProbe`, depois da limpeza dos streams e da
rajada do pool, concluiu 500 ciclos, verificou o estoque cheio, devolveu todos
os inimigos e encerrou sem avisos. O critério dos 500 ciclos está aprovado; a
sessão longa de 30 min continua pendente para validar estabilidade prolongada.

**Decisão de escopo do usuário, 2026-10-04:** considerar este ticket concluído
para avançar às builds do ticket 39, sem priorizar agora otimização adicional
de desempenho. Os critérios que ficaram sem validação ou foram reprovados
permanecem desmarcados e são limitações aceitas, não resultados aprovados:
sessão de 30 min, 60 fps e p99 no hardware de referência, alt-tab/resolução/
monitor, arquivos de configuração corrompidos e alocações gerenciadas fora do
fader. A medição na GeForce MX110 falhou o orçamento de 60 fps; o desempenho
alvo segue sem validação. O ticket 37 também continua com seus playtests de
balanceamento em aberto; a dispensa anterior permitiu prosseguir sem os dez
registros, mas não os registrou nem aprovou.
