# 29: O placar recompensa agressividade e a partida tem desfecho

**What to build:** um motivo para rejogar. Abater rápido e sem apanhar vale mais
pontos, e ao fim da partida uma tela mostra o que aconteceu e oferece o próximo
passo.

**Blocked by:** 28

**Status:** concluído

- [x] Cada abate soma pontos, e ondas mais avançadas valem mais
- [x] Abates em sequência rápida multiplicam o ganho
- [x] **Apanhar zera o multiplicador** — o placar premia quem arrisca e acerta
- [x] Limpar uma onda sem apanhar dá bônus
- [x] O placar aparece durante a partida e reage na hora
- [x] A tela final mostra pontos, ondas, abates, tempo e personagem
- [x] Dela dá para tentar de novo, trocar de personagem ou voltar ao menu
      (**só "tentar novamente" navega hoje**; os outros dois botões existem e
      ficam desabilitados até os tickets 30/31 criarem as telas de destino)
- [x] O melhor resultado de cada personagem é guardado entre sessões
- [x] A gravação do recorde não corrompe o arquivo se o jogo fechar no meio

## Comments

Zerar o multiplicador ao apanhar é o que impede a estratégia de ficar num canto
atirando. Sem isso o placar recompensa o oposto do que o jogo quer ensinar.

Gravação atômica não é preciosismo: o jogo fecha durante uma tela de resultado
mais vezes do que parece, e um perfil corrompido apaga todo o histórico.

## Implementação

### Placar (`ScoreKeeper`, POCO)

`score += ScoreValue × (1 + 0,1 × onda) × combo` (spec 10 §8). O combo sobe 0,1 por
abate dentro de 3 s do anterior (teto 3,0; o primeiro de uma sequência vale 1,0),
**zera ao apanhar** e expira sozinho quando a janela passa sem abate. Onda limpa
sem o jogador apanhar: +250. O relógio é do chamador, então tudo é xUnit
(`ScoreKeeperTests`, 11). `ScoreValue` é dado em `EnemyDefinition`/`.tres` (10 /
15 / 20 / 60 / 500, da tabela da spec 09 §6); janela e passo do combo, teto,
passo de onda e bônus de onda sem dano vivem em `data/score/horde_score.tres`
(`ScoreRulesDefinition`, entregue ao `ScoreKeeper` como o record puro
`ScoreRules`).

`HordeGameMode` alimenta o placar com o que já acontece na partida (abate,
`HealthComponent.Damaged` do jogador, início/fim de onda) e avisa por evento
(`GameEvents.ScoreChanged`) -- nunca por polling. `EnemyKilledEvent` ganhou
`ScoreValue` (mais um assinante que precisa do dado).

### HUD e tela de resultado

`ScoreDisplay` (novo widget no `Hud.tscn`, canto superior direito) mostra
"SCORE 4 250" e o combo ("x1.4", só quando há sequência), ouvindo `ScoreChanged`.

`ResultsScreen` (`scenes/ui/menus/`, um `CanvasLayer` adicionado pelo
`GameBootstrap` ao lado do HUD, pelo mesmo motivo -- regra 1 do CLAUDE.md) ouve
`GameEvents.MatchEnded` e mostra vitória/derrota, pontos, ondas, abates, tempo e
personagem. **Tentar novamente** recarrega a cena; **Trocar personagem** e
**Menu principal** dependem de telas dos tickets 31 e 30 -- ficam VISÍVEIS e
desabilitados ("Em breve") enquanto o caminho não existir, e ligam sozinhos
quando existir.

### Perfil e gravação atômica (`src/Persistence/`)

`ProfileStore` grava o `user://profile.cfg` da spec 14 §3 (estatísticas somadas +
melhor resultado por personagem, substituído só se o placar for MAIOR). Sem
engine: `ProfileSerializer` lê/escreve o INI à mão (o `ConfigFile` do Godot não
roda em xUnit) e `AtomicFile` grava num `.tmp` e troca pelo definitivo
(`File.Replace`). Leitura tolerante: arquivo corrompido vira perfil vazio, nunca
uma exceção. `ProfileStoreTests` (11) inclui a gravação INTERROMPIDA no meio (o
arquivo anterior fica intacto) e um `.tmp` velho não atrapalhando a próxima.
Gravado ao fim da partida (`HordeGameMode.EndMatch`); falha de disco loga e
segue, nunca derruba a tela de resultado.

### Um bug do ticket 28 achado no caminho

O cérebro ainda disparava `new EnemyKilledEvent()` sem argumentos -- um `record
struct` posicional aceita isso e o compilador não reclama --, então `IsBoss`
era SEMPRE falso e os reforços do chefe nunca paravam quando ele caía (só a
onda terminar os encerrava). O probe do ticket 28 não pegou porque a checagem
rodava antes do próximo lote (8 s). Corrigido, e o `HordeMatchProbe` agora
exige ver `EnemyKilled` com `IsBoss` na queda do chefe.

### Verificação

- `ScoreKeeperTests` (14), `ProfileStoreTests` (11), xUnit.
- `ScoreAndResultsProbe`: abates em sequência sobem o combo e o HUD mostra;
  apanhar volta o multiplicador a 1,0; ao morrer a tela de resultado aparece com
  os textos certos, os botões existem (tentar de novo habilitado), o perfil
  gravado tem a partida e o recorde e não deixa `.tmp`; e apertar "tentar
  novamente" recarrega a arena (a segunda instância do probe confirma, com a tela
  já escondida). `HordeMatchProbe` confere o placar mínimo da partida completa.
  Os probes de partida usam um perfil PRÓPRIO (`user://probe_profile.cfg`).

### O que não deu para verificar rigorosamente

- Os botões "Trocar personagem" e "Menu principal" só foram verificados
  **existindo e desabilitados**; a navegação em si depende dos tickets 30/31.
- Fechar o processo de verdade no meio de uma gravação: o xUnit simula a
  interrupção (o escritor lança no meio) e cobre o `.tmp` deixado para trás, mas
  não um `kill -9` real.
- A câmera lenta + fade da spec 10 §9 não foi feita (não está na lista do
  ticket) -- a tela aparece na hora.

### Code review

Duas sub-agents em paralelo (Standards e Spec) revisaram o diff staged. Achados
reais, corrigidos:

- **Constantes de balanceamento em `.cs`** (Standards, CLAUDE.md regra 4). Janela
  de combo, passo, teto, passo de onda e bônus estavam como `const` em
  `ScoreKeeper`. Agora vêm de `data/score/horde_score.tres`; o `ScoreKeeper`
  recebe um record `ScoreRules` (POCO, para o xUnit continuar dirigindo-o sem a
  engine), e um teste prova que os números vêm das regras e não de constantes.
- **Bônus de onda e tela de vitória só provados no xUnit** (Spec). O
  `HordeMatchProbe` agora conta 5 avisos de placar de exatos +250 depois de cada
  onda limpa sem apanhar, confere a tela "VITÓRIA" e que o HUD mostra o placar
  final.
- **Perfil corrompido era sobrescrito** (Spec): o ticket teme justamente
  "apagar todo o histórico". Agora um arquivo com conteúdo que não rende nada
  reconhecível é copiado para `.corrupt` antes da troca; `Load` também trata
  `UnauthorizedAccessException`. Dois testes novos.
- Borda da janela de combo (Standards, baixa confiança): testes novos para o
  abate exatamente em 3,0 s (encadeia) e em 3,01 s (quebra).

Aceito como está:

- **`!` sem comentário no `ScoreAndResultsProbe`**: mesmo padrão de todos os
  probes (campos resolvidos no `_Ready`, que aborta se nulos) -- vale a passada
  única sobre `src/Tools/` mencionada nos tickets anteriores, não este.
- **Membros públicos só lidos pelo probe** (`ScoreText`, `IsShowing`, botões):
  mesmo precedente do `WaveBanner.IsShowing`.
- **Dois canais para `MatchEnded`** (o `event` de `IGameMode`, da spec 10 §1, e o
  `GameEvents.MatchEnded` para a UI): o primeiro é o contrato do modo, o segundo
  é o que deixa a tela de resultado não conhecer o modo.
- **`RecordMatch` recebendo os quatro números soltos**: deixa o `ProfileStore`
  sem depender de `GameModeResult` (que usa `StringName`, da engine) e portanto
  testável em xUnit.
- **"Apanhar zera" provado por evento, não por causa**: o probe vê o
  multiplicador voltar a 1,0 logo depois do dano dentro da janela de 3 s, o
  que o xUnit já garante ser a causa; um dano de zero ou bloqueado não foi
  testado.
- **Multiplicador de onda no motor** (`CurrentWaveIndex` -> `RegisterKill`) só é
  coberto pelo xUnit; nenhum probe verifica um ganho de onda avançada.
