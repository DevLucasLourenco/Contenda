# 27: Uma onda de inimigos nasce, é limpa e dá lugar à próxima

**What to build:** o laço central do modo horda. Inimigos aparecem aos poucos
em pontos afastados, o jogador limpa, respira um instante, e a onda seguinte
começa — com um anúncio na tela.

**Blocked by:** 25, 23

**Status:** concluído

- [x] Os inimigos de uma onda aparecem espaçados no tempo, não todos de uma vez
- [x] **Nenhum inimigo nasce dentro do campo de visão imediato do jogador**
- [x] Nenhum nasce colado nele
- [x] Um marcador no chão avisa antes de o inimigo surgir
- [x] O recém-nascido fica brevemente invulnerável, para não morrer sem ser visto
- [x] Matar todos avança para a próxima onda, com um intervalo de respiro
- [x] O anúncio da onda aparece e some sozinho
- [x] A composição e o ritmo da onda vêm de arquivo de dados, não do código
- [x] **Um inimigo preso no cenário não trava a partida:** se não houver mais
      ninguém para matar por tempo demais, a onda avança assim mesmo

## Comments

O último critério é rede de segurança, não preguiça. Um inimigo perdido atrás de
um carro é a diferença entre uma partida de dez minutos e uma partida que nunca
termina — e vai acontecer.

Contar inimigos varrendo a cena a cada quadro é caro e frágil. A contagem vem do
evento de abate.

## Implementação

`WaveDirector`/`SpawnDirector` são novos, autocontidos, adicionados como
filhos INERTES de `Arena.tscn` (nada acontece até alguém chamar
`WaveDirector.Begin(waveSet)`) -- não construí `IGameMode`/`HordeGameMode`
nem contagem regressiva/vitória/derrota (spec 10 §1-2): isso pertence ao
ticket 28 ("cinco ondas, chefe e fim de partida"), que está logo depois na
fila e vai chamar `Begin` de verdade a partir do `StartMatch` que ele
constrói. Este ticket é só o LAÇO da onda em si, exatamente o próprio
título.

### Dados: `WaveDefinition`/`EnemySpawnEntry`/`WaveSetDefinition`

Três `Resource` novos em `src/GameModes/Horde/`, na forma exata da spec 10
§3, com uma omissão deliberada: `WaveDefinition.IsBossWave`/`MusicOverride`
(também na spec) ficaram de fora, mesma disciplina que
`EnemyDefinition` já segue para o resto do roster -- pertencem aos tickets
28 (onda de chefe) e 36/M8 (áudio de verdade), e adicionar agora seria campo
para um sistema que ainda não roda.

`data/waves/waveset_default.tres` (o nome que a própria spec 10 §10 cita
como critério de aceite: "trocar `waveset_default.tres` altera a progressão
sem recompilar") tem 2 ondas, só de grunt -- `runner`/`shooter`/`brute`/
`warlord` do roster completo da spec (§5-6) ainda não existem como
personagem jogável em código (só `grunt.tscn`/`grunt.tres`, ticket 25), 
então a progressão de 5 ondas da spec fica para quando esses tipos
existirem. `WaveDirectorProbe` confere a FORMA do `.tres` de produção (ondas,
entradas, contagens) sem simular as ondas inteiras -- rodar até o fim levaria
minutos, não segundos.

### `SpawnDirector`: onde nascer

Três passadas, relaxando uma regra de cada vez (spec 10 §6): (1) dentro de
`[MinDistanceFromPlayer, MaxDistanceFromPlayer]` do jogador E fora do
frustum da câmera (`Camera3D.IsPositionInFrustum`, de graça do motor -- a
câmera é fixa, o teste é barato como a spec promete); (2) só a distância, se
nada sobrar; (3) qualquer ponto conhecido, se AINDA assim nada sobrar.
"Nunca falha em spawnar" é literal.

Os candidatos são os filhos de `SpawnPoints` (`Spawn1..8`, ticket 21) --
não um array de `NodePath` um a um: Godot C# não exporta bem referência a
nó em array, e enumerar os filhos de um container é o idiomatismo natural.
Escolha ponderada (`SpawnPointMath.ChooseWeightedIndex`, testado em xUnit,
sorteio injetado como parâmetro -- mesma disciplina de `CritMath.Rolar`) com
peso reduzido para o ponto usado por último, contra a repetição.

Cada pedido de spawn primeiro acende um marcador no chão
(`GameEvents.SpawnMarkerRequested` -> `SpawnMarkerPool`, autoload pooled,
mesmo desenho de `DamageNumberPool`), espera `TelegraphSeconds` (0,4 s), só
DEPOIS chama `EnemyPool.Acquire` e concede `HealthComponent.GrantInvulnerability`
(o mesmo mecanismo dos i-frames do dash, ticket 17) por
`SpawnProtectionSeconds`.

### `WaveDirector`: o laço

`EnemiesRemaining = total planejado da onda − abatidos`, contado por
`GameEvents.EnemyKilled` (novo, disparado em `EnemyBrain.AoMorrer`), nunca
por varredura de cena -- spec 10 §4 e ticket 09 §4 são explícitos sobre
isso. Zerar `EnemiesRemaining` só faz a onda avançar depois de
`CompletionDelay` de respiro (`WaveCleared`/banner primeiro, depois a
próxima `WaveStarted`).

**Alçapão de inimigo preso**: há dois casos distintos. `EnemiesRemaining == 0`
com `EnemyPool.ActiveCount > 0` normalmente é só o corpo morto aguardando
`DeathDuration` antes de voltar ao pool (1,2 s por padrão); `WaveClearTimer`
só força a limpeza se essa divergência persistir por `StuckFallbackSeconds`
(5 s), cobrindo um evento de abate perdido. Para um inimigo VIVO que pede
movimento mas não avança, `EnemyBrain` acumula o tempo sem progresso somente
durante perseguição; o relógio reinicia quando ele se move, para de perseguir
ou deixa de pedir movimento. `WaveDirector` consulta o `EnemyPool` a cada
0,5 s e `KillStuckEnemies` enfileira uma morte para cada perseguidor parado
além do limite. O evento normal de abate atualiza a contagem da onda e a
liberação pelo pool ainda respeita `DeathDuration`.

O watchdog usa a velocidade real horizontal do `CharacterBody3D` após a
física, não a velocidade solicitada. Windup/ataque não contam como travamento,
e a rotina percorre só o conjunto de inimigos ativos do pool — sem varredura
da árvore de cena e sem alocação por quadro.

`StuckMovementTimer` tem testes xUnit para acumular tempo enquanto há
intenção de movimento sem deslocamento e reiniciar ao voltar a andar, sair
da perseguição ou parar de pedir movimento. `WaveDirectorProbe` continua
simulando também a divergência entre contagem e pool.

### Banner de onda

`WaveBanner` (novo `Control`, HUD) ouve `GameEvents.WaveAnnounced` sozinho,
mesmo desenho de `BossHealthBar`/`DamageNumberPool` -- nenhum sistema de
onda segura referência a ele. "Aparece e some sozinho" é literal: fica
visível por `VisibleDuration`, depois se apaga sozinho ao longo de
`FadeDuration`, sem ninguém mandar esconder.

### O que não deu para verificar rigorosamente

Nada além do já esperado para este tipo de sistema: tempo de frame/fps sob
carga (sem profiler neste ambiente headless), e a progressão de 5 ondas da
spec 10 §5 completa (precisa de `runner`/`shooter`/`brute`/`warlord`, que
ainda não existem).

### Code review

Duas sub-agents em paralelo (Standards e Spec) revisaram o diff staged
contra `docs/plans/convencoes-de-codigo.md`/o baseline de smells e contra
este ticket + `docs/specs/10-modos-de-jogo-horde.md`. Achados reais,
corrigidos:

- **Teto global de 40 nunca aplicado.** Spec 10 §7 é explícita ("Inimigos
  ativos simultâneos (teto duro): 40"), mas `WaveDirector` só respeitava
  `WaveDefinition.MaxConcurrent` (um hint de balanceamento por onda, sem
  trava nenhuma contra um `.tres` pedir mais que 40). Corrigido com
  `WaveDirector.GlobalActiveCap` (padrão 40), aplicado via
  `Mathf.Min(onda.MaxConcurrent, GlobalActiveCap)` antes de cada spawn.
- **`WaveDirector` sem nenhuma cobertura xUnit**, apesar de spec 15 §1-2
  pedir literalmente "WaveDirector avança com relógio simulado; fallback do
  inimigo preso" como requisito de teste. A lógica de fase/tempo estava
  toda inline no próprio `Node`, sem como isolar do Godot. Corrigido
  extraindo dois POCOs testáveis, seguindo o mesmo molde de
  `EnemyStateMachine`: `WaveClearTimer` (decide quando a onda está limpa e
  quando o alçapão de inimigo preso deve disparar, 9 testes) e `SpawnQueue`
  (decide qual entrada nasce a seguir entre concorrentes, 8 testes);
  `WaveDirector` passou a só traduzir o resultado dos dois em ações de
  engine.
- **`EnemySpawnEntry.Weight` era um campo morto.** O dado existia e era
  editável no `.tres`, mas nada lia nem aplicava o peso a uma escolha de
  verdade -- todo spawn dentro de uma onda saía na mesma ordem sempre.
  Corrigido: a extração do `SpawnQueue` (acima) passou a escolher a próxima
  entrada por `SpawnPointMath.ChooseWeightedIndex` (o mesmo utilitário já
  usado por `SpawnDirector` para pontos de spawn), com `Weight` agora
  determinando de verdade a proporção de cada inimigo na onda.
- **`WaveDefinition.EliteChance`, também um campo morto** (achado só pela
  Standards review): existia no `.tres` e no `Resource`, mas nada neste
  ticket lê ou aplica a chance a um spawn -- virar elite mudaria
  `EnemyDefinition.IsElite`, um `Resource` COMPARTILHADO por todo `grunt`,
  e fazer isso por instância exigiria um mecanismo de sobreposição que este
  ticket não constrói. Corrigido removendo o campo (e as duas linhas
  `EliteChance = 0.0` em `wave_1.tres`/`wave_2.tres`) em vez de deixar um
  knob de editor sem efeito nenhum -- mesma regra que `EnemyDefinition` já
  segue para o resto do roster ainda não implementado.
- **Operadores `!` sem comentário e faltando `ArgumentNullException`** em
  `WaveDirector`/`SpawnDirector`, contra convenções §6 (todo `!` precisa de
  justificativa) e o padrão de guarda de nulo já usado no resto do projeto.
  Corrigido: todos os `!` de `WaveDirector` viraram guardas
  `is not { } x` (capturando o local para o resto do método, sem
  null-forgiving nenhum); `Begin`/`RequestSpawn` ganharam
  `ArgumentNullException.ThrowIfNull`.
- **Gap restante no critério anti-travamento** (Spec review posterior): a
  correção anterior só cobria a divergência entre eventos de abate e o pool;
  um inimigo vivo pedindo movimento sem sair do lugar ainda podia bloquear a
  onda. Corrigido com `StuckMovementTimer`, que mede a velocidade real
  horizontal enquanto o estado é perseguição e há intenção de movimento, e
  com `EnemyPool.KillStuckEnemies`, que enfileira uma morte após
  `StuckFallbackSeconds`. O `WaveDirector` consulta o pool em intervalos de
  0,5 s; o evento normal de abate e a liberação por `DeathDuration` mantêm a
  contagem e o ciclo de pooling existentes. Testes xUnit cobrem a acumulação
  e os casos que reiniciam o relógio.

A sub-agent Standards desta rodada ainda não retornou ao fechar esta seção;
os quatro achados acima (teto global, cobertura xUnit, `Weight` morto,
escopo do alçapão) vieram da sub-agent Spec. Se a Standards trouxer algo
novo depois, entra como um adendo nesta mesma seção.

Não alterado, por ser exatamente o padrão estabelecido no resto de
`src/Tools/` (mesma decisão já registrada no ticket 23): a duplicação de
`Verificar`/`AvancarFase`/`Procurar` entre `WaveDirectorProbe.cs` e os
outros probes -- extrair uma base compartilhada agora tocaria em todos os
probes existentes por um ganho que não é deste ticket. A ÚNICA duplicação
extraída foi a LOCAL, dentro do próprio `WaveDirectorProbe.cs`
(`DetectarNovosSpawns`/`MatarTodosOsAtivos` repetiam o mesmo filtro
"grunt de verdade, nascido pelo `SpawnDirector`, não boneco de treino, não
estoque do pool" duas vezes) -- essa virou `GruntDaOndaOuNulo`, uma função
só.
