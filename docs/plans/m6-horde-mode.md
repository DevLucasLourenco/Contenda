# M6 — Horde Mode

**Objetivo:** o primeiro modo de jogo completo — 5 ondas, spawn inteligente,
pontuação, vitória e derrota.

**Esforço:** 5–6 dias · **Depende de:** M4 e M5

Spec: [10 — Modos de jogo e Horde](../specs/10-modos-de-jogo-horde.md)

## Entregáveis

- `IGameMode` + `GameModeBase` + `HordeGameMode`
- `WaveDirector` e `WaveDefinition` / `WaveSetDefinition`
- `SpawnDirector` com escolha de ponto fora do frustum
- Pontuação com multiplicador de combo
- Banner de onda, contador, tempo e score no HUD
- Tela de resultados

## Tarefas

### 1. Contrato de modo

- [ ] `IGameMode`, `GameModeState`, `GameModeConfig`, `GameModeResult`
- [ ] `GameModeBase` com o ciclo de vida e os eventos comuns
- [ ] Instanciado pelo `SceneRouter` como filho da arena
- [ ] A arena, a câmera e o personagem **não sabem** qual modo está ativo

### 2. `HordeGameMode`

- [ ] Instancia o jogador em `PlayerSpawn` a partir de `GameSession`
- [ ] `EnemyPool.Prewarm` de todos os tipos do `WaveSet` antes de começar
- [ ] Contagem regressiva 3-2-1
- [ ] Escuta `Health.Died` do jogador → `EndMatch(Victory: false)`
- [ ] `AllWavesCleared` → `EndMatch(Victory: true)`
- [ ] `PauseMatch` integrado ao pause do M7

### 3. Ondas

- [ ] `WaveDefinition`, `EnemySpawnEntry`, `WaveSetDefinition` (`Resource`)
- [ ] `data/waves/wave_01.tres` … `wave_05.tres` com a tabela da spec 10 §5
- [ ] `waveset_default.tres` agregando as cinco
- [ ] `WaveDirector` com `Begin`, `SkipToWave` (debug) e `Abort`
- [ ] Contagem por evento `EnemyKilled`, **nunca** varrendo a cena
- [ ] `CompletionDelay` entre ondas (respiro + regen de mana)
- [ ] **Safety:** `EnemiesRemaining == 0` com `ActiveCount > 0` por 5 s → loga e
      força a próxima onda

### 4. Spawn

- [ ] `SpawnDirector` com `MinDistanceFromPlayer` 12 m e `Max` 30 m
- [ ] Descartar pontos dentro do frustum da câmera (teste barato — câmera fixa)
- [ ] Relaxamento progressivo das regras; nunca falhar em spawnar
- [ ] Peso menor para o último ponto usado
- [ ] Marcador no chão + partículas 0.4 s antes de aparecer
- [ ] `SpawnProtectionSeconds` 0.5 s de invulnerabilidade
- [ ] Teto duro de 40 ativos; excedente fica na fila

### 5. Boss

- [ ] `warlord.tres` com 900 HP e 3 ataques alternados
- [ ] Reforços contínuos de grunt durante a onda 5
- [ ] Barra de vida dedicada no topo da tela
- [ ] Slow-mo de 1.2 s na morte

### 6. Pontuação

- [ ] `score += ScoreValue × waveMultiplier × comboMultiplier`
- [ ] `waveMultiplier` = 1 + 0.1 × índice da onda
- [ ] `comboMultiplier` +0.1 por abate em 3 s, teto 3.0, reseta ao levar dano
- [ ] Bônus de +250 por onda sem tomar dano
- [ ] Exibido no HUD com animação de incremento

### 7. HUD de modo

- [ ] `WaveBanner` — "ONDA N" no centro por 1.5 s
- [ ] Contador de onda, inimigos restantes, tempo, score, multiplicador
- [ ] Tudo alimentado por eventos do modo, sem polling

### 8. Resultados

- [ ] `ResultsScreen.tscn` com score, ondas, abates, tempo, personagem
- [ ] Botões: Tentar novamente · Trocar personagem · Menu principal
- [ ] Gravação do recorde em `user://profile.cfg` (escrita atômica)
- [ ] Slow-mo + fade antes de abrir

### 9. Debug

- [ ] `--wave=N` para começar em qualquer onda
- [ ] Tecla de debug para matar todos os inimigos ativos (testar transições)
- [ ] Log de cada onda: composição, duração, dano sofrido

## Critérios de aceite

- [ ] As 5 ondas rodam do início ao fim sem travar
- [ ] Nenhum inimigo nasce dentro do campo de visão imediato
- [ ] Matar todos avança a onda; morrer abre a derrota
- [ ] Um inimigo preso não impede o avanço (fallback de 5 s)
- [ ] O boss aparece na onda 5 com barra dedicada
- [ ] O score reflete agressividade (combo sobe com abates rápidos)
- [ ] Trocar `waveset_default.tres` altera toda a progressão sem recompilar
- [ ] Uma partida completa leva 8–12 minutos

## Tuning

| Parâmetro | Efeito |
|---|---|
| `SpawnInterval` | pressão momento a momento |
| `MaxConcurrent` | pico de caos |
| `CompletionDelay` | respiro entre ondas |
| HP dos inimigos | tempo de morte, sensação de peso |
| `ScoreValue` | o que o jogo recompensa |

**Sensação alvo:** onda 1 confortável, onda 3 tensa, onda 4 com quase-morte,
onda 5 exigindo transformação. Se der para vencer sem transformar, o custo de
mana está barato demais — ou o boss, fraco demais.

## Riscos

| Risco | Mitigação |
|---|---|
| Onda travada por inimigo perdido | safety de 5 s + fallback de navegação do M5 |
| Spawn injusto (inimigo nas costas) | filtro de frustum + distância mínima + telegrafia de spawn |
| Ritmo monótono | variar composição, não só quantidade; `shooter` muda o comportamento do jogador |
| Boss como esponja de HP | 3 ataques distintos e reforços — não só mais vida |
