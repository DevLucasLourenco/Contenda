# 28: Cinco ondas, um chefe, e a partida termina

**What to build:** a partida completa. Dificuldade crescente ao longo de cinco
ondas, um chefe na última, e um fim de verdade — vitória se ele cair, derrota se
o jogador cair antes.

**Blocked by:** 27, 24

**Status:** implementado; pendente playtest para validar duração de 8 a 12 minutos

- [x] As cinco ondas rodam do início ao fim sem travar
- [x] A composição varia, não só a quantidade: aparecem tipos que **mudam** o
      comportamento do jogador, não apenas mais do mesmo
- [x] Um elite aparece antes do chefe e é sentido como salto de dificuldade
- [x] O chefe tem mais de um golpe e reforços chegando, em vez de só muita vida
- [x] A praça funciona como funil na onda do elite e na do chefe
- [x] Morrer encerra a partida em derrota; limpar a última onda, em vitória
- [x] Trocar o arquivo do conjunto de ondas muda toda a progressão sem recompilar
- [ ] Uma partida completa leva entre oito e doze minutos

## Comments

Chefe que é só uma esponja de vida cansa em trinta segundos. Ele reusa a mesma
máquina de estados dos outros inimigos, com golpes alternados e parâmetros
diferentes — nenhuma classe nova.

A onda 5 deve ser inviável sem transformação. Se der para vencer sem se
transformar, o custo de mana está barato demais ou o chefe está fraco.

## Implementação

### Roster: dado, não classe

`runner`, `shooter`, `brute` e `warlord` são só `.tres` + uma `.tscn` fina por
espécie (`scenes/characters/Enemy*.tscn` herdam `EnemyGrunt.tscn` e trocam o
`CharacterDefinition`, o tamanho/cor do corpo e a altura da barra) -- nenhuma
classe de inimigo nova, como a spec 09 §6 pede. Números da tabela da spec:

| Espécie | HP | Vel. | Arma | Papel |
|---|---|---|---|---|
| `runner` | 25 | 5,4 | garras, 8 | pressiona |
| `shooter` | 30 | 2,8 | pistola hitscan, 12, 14 m | força movimentação |
| `brute` | 160 | 4,2 | maça, 25, knockback 6 | elite (`IsElite`) |
| `warlord` | 900 | 3,0 | martelo de 3 golpes, 35 | chefe (`IsBoss`) |

Três mecanismos pequenos tornaram isso possível sem tocar na arquitetura:

- **Atirador com a MESMA arma do jogador.** `HitscanWeapon` lê a mira do
  `TargetingComponent`, que só o mouse alimentava. `TargetingComponent.SetAim`
  + um passo `2b` em `CharacterController._PhysicsProcess` repassam a mira que o
  `EnemyBrain` já calculava no `IntentFrame` -- e o `EnemyShooter.tscn` só ganha
  um `TargetingComponent`. Nenhum código de "tiro de inimigo".
- **Chefe com mais de um golpe: `EnemyDefinition.ComboHits`.** A arma do
  warlord tem 3 `MeleeComboStep` (dano/janela/avanço próprios; o terceiro é a
  investida, `ForwardStep = 2,5`) e o cérebro só insiste em pedir o próximo passo
  até `ComboHits` -- `MeleeCombo.TryStart` já recusa até a janela abrir, então
  pedir todo quadro é seguro. Uma leitura mais literal de "3 ataques
  alternados" (ciclar entre golpes distintos entre um ataque e o seguinte) não
  cabe no `MeleeCombo`: a cadeia zera quando a janela de encadeamento acaba, e o
  estado `Attack` só termina quando ela acaba -- então o "alternado" virou
  "encadeado", numa mesma investida do chefe.
- **`EnemyDefinition.Scene`/`PoolSize`.** O pool só prewarmava o grunt do boot;
  cada espécie agora diz de onde nasce e quantas instâncias reserva (spec 10 §7:
  30/20/8/1). `HordeGameMode` prewarma o que o conjunto de ondas usa e o
  `EnemyPool` ainda não conhece. A cena NÃO referencia a própria
  `EnemyDefinition` de volta (o pool já força a definição do parâmetro por cima
  da da cena), senão o `.tres` e a `.tscn` seriam referências circulares.

### Composição e o espaço da cidade

`data/waves/waveset_default.tres` segue a tabela da spec 10 §5 (5 ondas). Cada
onda traz algo que muda o que o jogador faz, não só mais do mesmo: runner (onda
2), shooter (3), brute (4), chefe (5). `EnemySpawnEntry.DelayBeforeFirst`
escalona a chegada (o brute entra ~8 s depois do começo da onda 4, com o resto
já em combate; os grunts do chefe só 6 s depois dele).

Spec 17 §6 ("o espaço muda de função conforme a onda") virou dado:
`EnemySpawnEntry.SpawnGroup` (e `WaveDefinition.ReinforcementSpawnGroup`)
restringe os candidatos do `SpawnDirector` aos pontos de um grupo de nós -- ruas
laterais na onda 3 (pinça), `spawn_plaza` (Spawn7, no centro da praça) para o
brute e o chefe, `spawn_alley` para os reforços. Grupo vazio, ou grupo sem ponto
nenhum, cai em todos os pontos: "nunca falha em spawnar" continua valendo. Em
cima dos grupos, as três passadas de relaxamento (distância + frustum) do ticket
27 funcionam igual.

### Reforço contínuo

`WaveDefinition.ReinforcementEnemy/Batch/Interval/SpawnGroup`: enquanto o chefe
da onda vive, chega um lote a cada `Interval` (`ReinforcementClock`, POCO
testado em xUnit, mesma disciplina do `WaveClearTimer`). Reforços entram na
contagem da onda (`_totalDaOnda`) -- a onda só termina quando TODOS caem -- e
respeitam o teto de inimigos (`min(MaxConcurrent, GlobalActiveCap)`): um lote
que não cabe espera, nunca é descartado. "Chefe vivo" vem de
`EnemyKilledEvent.IsBoss` (campo novo, agora que há um assinante que precisa
dele) e do próprio `EnemyDefinition.IsBoss` da entrada; não existe um
`IsBossWave` duplicado na onda.

### `HordeGameMode` e o fim da partida

`IGameMode`/`GameModeState`/`GameModeConfig`/`GameModeResult` (spec 10 §1) +
`HordeGameMode` (nó filho da arena): `StartMatch` aguarda o prewarm padrão do
`EnemyPool`, prepara as espécies extras e chama `WaveDirector.Begin`. A cena
`HordeMatch.tscn` dispara `StartMatch` no `_Ready`, então adiar o prewarm do
roster até o pool indicar `IsReady` evita que o grunt sem `Scene` gere erros
antes do prewarm assíncrono do autoload. Vitória = limpar a última onda (na
hora, sem esperar o respiro final); derrota = `HealthComponent.Died` do jogador.
`EndMatch` para o `WaveDirector`, guarda o resultado em `GameSession.LastResult`
e dispara `MatchEnded` -- a tela de resultado é o ticket 29.

O modo fica inerte em `Arena.tscn` (é a cena principal e a base de todos os
probes); `scenes/arena/HordeMatch.tscn` herda a arena e liga `AutoStart` -- é a
cena para jogar de verdade (F6).

Fora de escopo, deliberadamente: `PauseMatch`/`Loading` (ticket 32), contagem
3-2-1 (HUD, ticket 33), pontuação (ticket 29 -- `GameModeResult.Score` é sempre
0), tela de resultado/câmera lenta (ticket 29).

### Verificação

- `ReinforcementClockTests` (7), xUnit.
- `EnemyRosterProbe`: o runner passa de 5 m/s enquanto o grunt fica em 3,4; o
  shooter tira vida do jogador a ~13 m; o brute tira 25 num golpe; o warlord
  encadeia até o passo 3 do combo e se anuncia em `GameSession.BossBody`.
- `HordeMatchProbe` (vitória): um "jogador" automático joga o
  `waveset_default.tres` de verdade, a 3x de velocidade -- 5 ondas limpas, 52
  abates, a composição por onda confere (só grunt; +runner; +shooter; +brute;
  warlord), brute e chefe nascem na praça, a onda 3 nasce pelas ruas laterais,
  chegam reforços enquanto o chefe vive e nenhum depois dele cair, e
  `GameSession.LastResult` guarda a vitória. `HordeDefeatProbe`: morrer encerra
  a partida em derrota e para o `WaveDirector`.
- Inicialização real de `HordeMatch.tscn`: o auto-start prepara o roster depois
  do `EnemyPool`; a partida abre sem erros de espécie ausente no log.

### O que não deu para verificar rigorosamente

- **"Uma partida completa leva entre oito e doze minutos."** O bot mata cada
  inimigo no quadro em que ele aparece, então o probe só prova o PISO (~78 s de
  spawn e respiro); o resto do tempo é o jogador matando de verdade -- isso é
  balanceamento e precisa de gente jogando (ticket 37). O `HordeMatch.tscn`
  existe para isso.
- **"A onda 5 deve ser inviável sem transformação."** Não há transformação no
  código ainda (`data/transformations/` está vazio), então não dá nem para
  testar. Fica como critério de balanceamento do ticket 37.
- **"Um elite é sentido como salto de dificuldade"** e **"a praça funciona como
  funil"**: o probe prova que o brute (160 HP, 25 de dano, knockback alto) e o
  chefe NASCEM na praça, não que lutar lá é mais perigoso e mais lucrativo --
  isso é sensação de jogo.
- Balanceamento dos números do roster (tirados da tabela da spec, sem playtest).

### Code review

Duas sub-agents em paralelo (Standards e Spec) revisaram o diff staged. Achados
reais, corrigidos:

- **Decisão de reforço fora do POCO testado** (Standards, spec 15 §1-2). O
  `ReinforcementClock` tinha testes, mas "o chefe vive?" e "cabe sob o teto de
  inimigos?" ficavam inline em `WaveDirector.TickReforcos`. Corrigido:
  `ReinforcementClock.TryDeliver` decide tudo (devido + ativo + cabe; sem
  espaço o lote espera, nunca é descartado), com 3 testes novos.
- **"Trocar o arquivo do conjunto muda a progressão" só era verdade por
  construção** (Spec). Nenhum probe trocava o conjunto. Corrigido:
  `HordeSwapProbe` (cenário `swap` do `HordeMatchProbe`) injeta um conjunto de
  uma onda por `IGameMode.Initialize` e confere que a partida segue o conjunto
  novo (1 onda, 2 abates, vitória).
- Menores: `HordeGameMode.Id` alocava um `StringName` por leitura (agora
  `static readonly`); o doc de `IGameMode` dizia que `Starting` ficava de fora do
  estado, mas ele é usado (corrigido); um `_contexto!` novo em `EnemyBrain`
  virou `_contexto?.`.

Achados aceitos como estão, com o motivo:

- **`!` sem comentário nos dois probes novos.** É o padrão de todos os probes
  existentes (campos resolvidos em `_Ready`, que aborta se nulos); mudar só estes
  dois os tornaria inconsistentes com os outros 19. Se a convenção §6 for para
  valer nos probes, é uma passada única sobre `src/Tools/`, não deste ticket.
- **Dano do warlord vs. tabela da spec 09 §6.** A tabela diz "35"; a arma tem
  `BaseDamage = 35` e os passos do combo multiplicam (0,7 / 0,9 / 1,5), então a
  cadeia inteira tira ~108 e cada golpe individual sai de ~25 a ~53. É uma leitura
  do "35" como dano-base da arma, coerente com o resto do roster (todos usam
  `BaseDamage` × multiplicador); os números finais são do ticket 37.
- **Telegrafia dos golpes 2 e 3 do chefe.** Só o primeiro passa pelo windup de
  1,0 s (spec 09 §7, "windup >= 0,3 s em todos"); os seguintes só têm o intervalo
  entre janelas do `MeleeComboStep` (0,45 s / 0,6 s de início de acerto). Sem
  animação (ticket 34) não dá para julgar a legibilidade, e o decal de área no
  chão (spec 09 §7, brute/warlord) também não existe -- nenhum dos dois está na
  lista do ticket 28, mas os dois pesam para o "elite sentido como salto".
- **"Alternados"** virou "encadeados" (explicado em Implementação): o
  `MeleeCombo` não alterna entre ataques distintos entre um pedido e o seguinte.
- **Funil da praça.** É só `spawn_plaza` (um ponto). Nada muda no terreno nem na
  navegação; o funil é o próprio desenho da praça (spec 17 §3), e "lutar lá é mais
  perigoso e mais lucrativo" fica para playtest. Vale saber que o relaxamento de
  distância do `SpawnDirector` deixa um spawn na praça aparecer colado num jogador
  que também esteja nela.
- **Reforços podem passar fome sob o teto** (`MaxConcurrent = 12` na onda 5): um
  campo cheio adia o lote em vez de descartá-lo, e o probe não mede quanto isso
  acontece com jogador de verdade -- ticket 37.
- **Escopo:** `Scene`/`PoolSize`, `SetAim` e `EnemyKilledEvent.IsBoss` não estão no
  ticket, mas cada um é o mínimo para uma parte dele (prewarm do roster, atirador,
  parar reforços quando o chefe cai); nenhum tem campo sem leitor.
