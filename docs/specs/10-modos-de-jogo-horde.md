# 10 — Modos de jogo e Horde

## 1. `IGameMode`

O modo é um nó filho da arena. Ele define regras de vitória/derrota, spawn e
pontuação. A arena, a câmera e o personagem não sabem qual modo está ativo.

```csharp
public interface IGameMode
{
    StringName Id { get; }
    GameModeState State { get; }

    void Initialize(GameModeConfig config);
    void StartMatch();
    void PauseMatch(bool paused);
    void EndMatch(GameModeResult result);

    event Action<GameModeState> StateChanged;
    event Action<GameModeResult> MatchEnded;
}

public enum GameModeState { Loading, Starting, Playing, Paused, Ending, Ended }

public readonly record struct GameModeResult(
    bool Victory, int Score, int WavesCleared,
    int EnemiesKilled, float DurationSeconds, StringName CharacterId);
```

Roadmap de modos — todos entram sem alterar o núcleo:

```
IGameMode
   ├── HordeGameMode        ← MVP
   ├── OneVsOneGameMode     (pós-MVP)
   ├── BossRushGameMode     (pós-MVP)
   ├── SurvivalGameMode     (pós-MVP)
   ├── TournamentGameMode   (pós-MVP)
   └── CoopHordeGameMode    (pós-MVP, exige rede)
```

## 2. `HordeGameMode`

```
StartMatch
   ├─ instancia o personagem em PlayerSpawn a partir de GameSession
   ├─ EnemyPool.Prewarm de todos os tipos do WaveSet
   ├─ contagem regressiva 3-2-1
   └─ WaveDirector.Begin(waveSet)
         │
         ├─ para cada WaveDefinition:
         │     banner "ONDA N"
         │     SpawnDirector distribui os spawns ao longo do tempo
         │     aguarda ActiveCount == 0 (ou objetivo da onda)
         │     CompletionDelay (respiro + regen de mana)
         │
         └─ fim do WaveSet -> Victory
   player morre -> EndMatch(Victory: false)
```

Derrota: morte do jogador. Sem vidas extras nem revive no MVP.
Vitória: completar a última onda do `WaveSet`.

## 3. `WaveDefinition`

```csharp
[GlobalClass]
public partial class WaveDefinition : Resource
{
    [Export] public string DisplayName;             // "ONDA 3"
    [Export] public EnemySpawnEntry[] Entries;
    [Export] public float SpawnInterval   = 0.8f;   // s entre spawns
    [Export] public int   MaxConcurrent   = 12;     // teto desta onda
    [Export] public float EliteChance     = 0f;     // 0..1
    [Export] public float CompletionDelay = 4f;     // s antes da próxima
    [Export] public bool  IsBossWave      = false;
    [Export] public AudioStream MusicOverride;
}

[GlobalClass]
public partial class EnemySpawnEntry : Resource
{
    [Export] public EnemyDefinition Enemy;
    [Export] public int Count = 5;
    [Export] public float Weight = 1f;     // distribuição na ordem de spawn
    [Export] public float DelayBeforeFirst = 0f;
}

[GlobalClass]
public partial class WaveSetDefinition : Resource
{
    [Export] public string DisplayName;             // "Horde — Padrão"
    [Export] public WaveDefinition[] Waves;
    [Export] public bool LoopWithScaling = false;   // endless pós-MVP
    [Export] public float ScalingPerLoop = 1.15f;
}
```

## 4. `WaveDirector`

```csharp
public sealed class WaveDirector : Node
{
    public int CurrentWaveIndex { get; }
    public int EnemiesRemaining { get; }
    public WaveDefinition CurrentWave { get; }

    public void Begin(WaveSetDefinition set);
    public void SkipToWave(int index);        // debug
    public void Abort();

    public event Action<WaveDefinition, int> WaveStarted;
    public event Action<WaveDefinition, int> WaveCleared;
    public event Action AllWavesCleared;
}
```

- `EnemiesRemaining` = ainda por spawnar + ativos.
- Contagem por evento `EnemyKilled` do `GameEvents`, **não** por varredura de
  cena a cada frame.
- Segurança: se `EnemiesRemaining == 0` mas `ActiveCount > 0` por mais de 5 s,
  loga aviso e força a próxima onda (um inimigo preso no cenário não pode travar
  a partida).

## 5. Progressão do `WaveSet` padrão

| Onda | Composição | Máx. simultâneos | Intervalo | Observações |
|---|---|---|---|---|
| 1 | 5 grunt | 5 | 1.0 s | ensina o básico |
| 2 | 6 grunt + 2 runner | 8 | 0.9 s | introduz pressão |
| 3 | 8 grunt + 3 runner + 1 shooter | 12 | 0.8 s | força movimentação |
| 4 | 9 grunt + 4 runner + 2 shooter + **1 brute** | 15 | 0.7 s | elite |
| 5 | **warlord** + 6 grunt (reforço contínuo) | 12 | 1.2 s | **boss** |

Fecha o MVP com ~8–12 minutos de partida. Ondas 6+ entram pós-MVP via
`LoopWithScaling`.

Requisito da concepção — "5–10 inimigos simultâneos" — é atendido nas ondas 1–3;
o teto técnico é 40 (ver §7).

## 6. `SpawnDirector`

```csharp
public sealed class SpawnDirector : Node
{
    [Export] public Marker3D[] SpawnPoints;
    [Export] public float MinDistanceFromPlayer = 12f;
    [Export] public float MaxDistanceFromPlayer = 30f;
    [Export] public float SpawnProtectionSeconds = 0.5f;

    public EnemyController Spawn(EnemyDefinition def);
}
```

### Escolha do ponto

1. Filtra `SpawnPoints` por distância ao jogador dentro de
   `[Min, Max]`.
2. Descarta os que estão dentro do frustum da câmera (não aparecer do nada na
   frente do jogador). Como a câmera é fixa, isso é um teste barato.
3. Se nenhum sobrar, relaxa a regra 2; depois a regra 1. Nunca falha em spawnar.
4. Aleatório entre os candidatos, com peso menor para o ponto usado por último.

### Efeito de spawn

Marcador no chão + partículas 0.4 s antes do inimigo aparecer, e
`SpawnProtectionSeconds` de invulnerabilidade para o inimigo — evita que ele
nasça dentro de um Spin Slash e morra sem o jogador perceber.

## 7. Limites de performance

| Limite | Valor |
|---|---|
| Inimigos ativos simultâneos (teto duro) | **40** |
| Alvo por onda no MVP | 5–15 |
| Instâncias pré-alocadas no pool | ~119 |
| Orçamento de frame para IA | 3 ms |

Se uma onda pedir mais que o teto, o `SpawnDirector` segura o excedente na fila
até alguém morrer.

## 8. Pontuação

```
score += enemy.ScoreValue × waveMultiplier × comboMultiplier
```

- `waveMultiplier` = 1 + 0.1 × índice da onda.
- `comboMultiplier` sobe 0.1 por abate dentro de 3 s do anterior, teto 3.0,
  reseta ao levar dano. Recompensa agressividade — o oposto de "ficar seguro no
  canto".
- Bônus de onda sem tomar dano: +250.

## 9. Fim de partida

```
Derrota                              Vitória
  slow-mo 1.5 s                        slow-mo 1.5 s
  fade to black                        fanfarra
        ↓                                    ↓
   ResultsScreen  ← GameModeResult ─────────┘
        ├─ Score / Ondas / Abates / Tempo / Personagem
        ├─ [ TENTAR NOVAMENTE ]   (recarrega a arena, mesmo personagem)
        ├─ [ TROCAR PERSONAGEM ]  (volta ao Character Select)
        └─ [ MENU PRINCIPAL ]
```

`GameSession.LastResult` guarda o resultado; melhor pontuação por personagem vai
para `user://profile.cfg` (ver [spec 14](14-configuracoes-persistencia-e-build.md)).

## 10. Critérios de aceite (M6)

- [ ] As 5 ondas rodam do início ao fim sem travar.
- [ ] Nenhum inimigo nasce dentro do campo de visão imediato do jogador.
- [ ] Matar todos avança a onda; morrer abre a tela de derrota.
- [ ] Um inimigo preso no cenário não impede o avanço (fallback de 5 s).
- [ ] Trocar `waveset_default.tres` altera toda a progressão sem recompilar.
- [ ] Adicionar `BossRushGameMode` exigiria só uma nova classe `IGameMode`.
