# 13: O jogo registra sequências de WASD sem atrapalhar o movimento

**What to build:** andar segurando uma tecla continua andando; **tocar** a tecla
grava um símbolo numa fila curta que expira sozinha. É a base da mecânica
assinatura do jogo, e precisa conviver com o movimento sem que um estrague o outro.

**Blocked by:** 05, 03

**Status:** CONCLUÍDO — só a parte de POCO/dados do M3 Tarefas 1-2. Ligar isto
ao teclado de verdade é o ticket 14 (M3 Tarefa 9), junto com o
`AbilityComponent` que ainda não existe.

- [x] Andar segurando W por três segundos grava **um** símbolo, não sessenta
- [x] Tocar W duas vezes rápido grava dois símbolos e o personagem mal sai do lugar
- [x] Um símbolo isolado some sozinho depois de menos de um segundo
- [x] A sequência inteira é descartada se o jogador parar no meio
- [x] Morrer, tomar atordoamento ou pausar limpa a fila — a chamada existe
      (`Clear()`); QUEM chama é o `AbilityComponent` do ticket 14
- [x] Existe uma consulta que responde "qual habilidade casa com esta sequência?"
      e outra que responde "quais ainda podem casar se ele continuar digitando?"
- [x] Duas habilidades declarando a mesma sequência **quebram o carregamento**
      com mensagem clara, em vez de uma delas nunca disparar
- [x] Testes cobrindo expiração, capacidade, casamento exato, prefixo e ambiguidade

## Comments

Este é o ticket de maior risco do projeto (R1 e R4). Se em teste as habilidades
saírem sem intenção durante o reposicionamento, o remédio é exigir intervalo
mínimo entre símbolos iguais — não encurtar a janela, que piora a execução
proposital.

---

**Concluído em 2026-09-05** (a fatia de POCO — ver status acima).

**O que foi construído:**

- [`CommandBuffer`](../../../src/Input/CommandBuffer.cs) — POCO, como o
  `MeleeCombo`. Duas regras de expiração INDEPENDENTES, spec 03 §5:
  `TokenLifetime` é janela deslizante por símbolo (o mais velho sai sozinho,
  sem derrubar o resto); `SequenceTimeout` é teto sobre a sequência inteira
  desde o primeiro símbolo. Sob a sintonia padrão (0,7 s / 1,2 s) a primeira
  quase sempre dispara antes da segunda ter chance — a segunda existe como
  rede de segurança para quando alguém tunar `TokenLifetime` para cima sem
  lembrar do teto. Testadas em separado, com sintonias que isolam cada uma.
- A disciplina "só grava na borda de subida" é do CHAMADOR
  (`PlayerInputController`, ainda não escrito), não do buffer — `Push` grava
  o que mandarem gravar, sem saber a diferença entre segurar e tocar. É por
  isso que "segurar W por 3 s grava 1 símbolo" tem teste xUnit direto: chamar
  `Push` uma vez JÁ É o que "tocar uma vez" significa daqui pra baixo.
- [`AbilityComboResolver<T>`](../../../src/Input/AbilityComboResolver.cs) é
  GENÉRICO, não fixado em `AbilityDefinition`. Motivo: `AbilityDefinition`
  ainda não existe (nasce no ticket 14) e será um `Resource` — construí-lo
  fora do processo do Godot derruba o processo, o mesmo problema de
  `StringName` que já apareceu em `DamageInfo.SourceTag` e
  `StatModifier.Source`. Deixar o resolver genérico permite testar a trie
  inteira (exato, prefixo, ambiguidade, sequência vazia) com um `record` comum
  em vez de esperar o ticket 14. Quando `AbilityComponent` chegar, ele só
  instancia `AbilityComboResolver<AbilityDefinition>` — nenhuma linha desta
  classe muda.
- Lança `InvalidOperationException` com os nomes dos dois itens em conflito na
  mensagem (via um `descreverPara` opcional, com fallback pra `ToString()`) —
  "quebra com mensagem clara", não só uma exceção genérica.

**Deliberadamente fora deste ticket** (M3 Tarefa 9, ticket 14): ligar
`PlayerInputController` para chamar `Push` na borda de subida de
`move_up/down/left/right`; instanciar `CommandBuffer` e
`AbilityComboResolver<AbilityDefinition>` dentro de um `AbilityComponent`;
chamar `Clear()` a partir de `Health.Died`, atordoamento e pausa. Nenhuma
dessas pontas tem onde morar ainda — não existe `AbilityComponent`, e a
tentação de criar um agora só para "terminar de verdade" teria sido
exatamente o tipo de acoplamento prematuro que o design genérico acima evita.

**Verificação:** 202 testes xUnit (31 novos), `ExportRelease` limpo,
verificador anti-2D limpo. Sem sonda nova — não há nada para ligar na árvore
real ainda; as sondas existentes (`CombatProbe`, `RevolverProbe`, `ManaProbe`,
`HudProbe`, `ImpactProbe`) foram re-executadas como checagem de regressão, já
que nenhum arquivo de cena ou wiring foi tocado.

**Não verificado nesta sessão:** o risco R1 do próprio ticket — habilidades
saindo sem querer durante o reposicionamento em combate real. Só aparece
quando o teclado estiver de fato ligado ao buffer, no ticket 14.
