# 14 — Configurações, persistência e build

## 1. Menu de configurações

O requisito pede "configurações gerais". Escopo do MVP, em quatro abas.

### Vídeo

| Opção | Valores |
|---|---|
| Modo de janela | Janela · Tela cheia · Tela cheia sem borda |
| Resolução | lista de `DisplayServer.ScreenGetModeList` |
| VSync | Ligado · Desligado · Adaptativo |
| Limite de FPS | 60 · 120 · 144 · 240 · Ilimitado |
| Qualidade de sombra | Baixa · Média · Alta |
| SSAO / SSIL | Ligado · Desligado |
| Anti-aliasing | Desligado · FXAA · TAA · MSAA 2x/4x |
| Escala de renderização | 0.5 – 1.0 |

### Áudio

Master · Música · SFX · UI · Ambience (0–100, em dB no bus) + "Silenciar em
segundo plano".

### Controles

- Rebind de todas as ações da [spec 03](03-input-comandos-e-combos.md), exceto
  `pause` e `ui_*`.
- Detecção de conflito: rebind para uma tecla já usada avisa e pede confirmação
  (a segunda ação fica sem bind).
- Sensibilidade do mouse (afeta mira futura com gamepad).
- Inverter scroll da seleção de forma.
- "Restaurar padrões".

### Jogo

| Opção | Padrão |
|---|---|
| Intensidade de screen shake | 100% (0–150%) |
| Mostrar números de dano | Ligado |
| Mostrar guia de combos | Ligado |
| Janela do buffer de comandos | Curta 0.5 s · **Normal 0.7 s** · Longa 0.9 s |
| Idioma | pt-BR · en-US |
| Dificuldade | Normal · Difícil (pós-MVP) |

> A janela do buffer como opção de acessibilidade é deliberada: o sistema de
> comandos é a barreira de entrada do jogo, e a tolerância de timing varia muito
> entre jogadores.

## 2. `GameSettings` e persistência

```csharp
public sealed class GameSettings
{
    public VideoSettings Video { get; set; }
    public AudioSettings Audio { get; set; }
    public Dictionary<StringName, InputBinding[]> Bindings { get; set; }
    public GameplaySettings Gameplay { get; set; }
    public int Version { get; set; } = 1;
}

public static class SettingsStore
{
    public static GameSettings Load();          // aplica defaults se ausente
    public static void Save(GameSettings s);
    public static void Apply(GameSettings s);   // escreve no DisplayServer/InputMap/AudioServer
}
```

- Arquivo: `user://settings.cfg`, via `ConfigFile` (texto, editável, sem
  dependência externa).
- `Version` permite migração; ler uma versão desconhecida cai para os padrões
  em vez de quebrar.
- Salvar acontece ao **aplicar**, não a cada mexida de slider.
- `Apply` é idempotente e roda no boot (`GameBootstrap`) antes da primeira cena.

Caminhos reais de `user://`: `%APPDATA%\Godot\app_userdata\Contenda` (Windows),
`~/.local/share/godot/app_userdata/Contenda` (Linux),
`~/Library/Application Support/Godot/app_userdata/Contenda` (macOS).

## 3. Perfil e recordes

Não há progressão entre partidas no MVP, mas guardamos estatísticas — custa
pouco e dá razão para rejogar.

```
user://profile.cfg
[stats]
total_matches = 12
total_kills = 843
total_playtime_seconds = 5241

[best.swordsman]
score = 18400
waves = 5
time_seconds = 512

[best.gunslinger]
score = 15100
waves = 4
```

Escrito ao fim de cada partida (vitória ou derrota). Nunca durante o gameplay.
Gravação atômica: escreve em `.tmp` e renomeia, para não corromper em crash.

## 4. Estado de sessão (não persistido)

`GameSession` (autoload) carrega apenas o que a partida corrente precisa:

```csharp
public sealed partial class GameSession : Node
{
    public StringName SelectedModeId { get; set; }
    public CharacterDefinition SelectedCharacter { get; set; }
    public WaveSetDefinition SelectedWaveSet { get; set; }
    public GameModeResult? LastResult { get; set; }
    public void ResetToMenu();
}
```

Sem save de partida em andamento no MVP — uma corrida de horda dura ~10 min.

## 5. `SceneRouter`

```csharp
public sealed partial class SceneRouter : Node
{
    public void GoTo(string scenePath, bool showLoading = true);
    public void Reload();
    public event Action<string> SceneChanged;
}
```

- `ResourceLoader.LoadThreadedRequest` + polling de progresso, com
  `LoadingScreen.tscn` mostrando barra real.
- Despausa e limpa `GetTree().Paused` em toda transição (bug clássico: sair do
  pause para o menu e o menu nascer pausado).
- Fade out/in de 0.25 s.

## 6. Argumentos de linha de comando (debug)

| Flag | Efeito |
|---|---|
| `--skip-menu` | vai direto para a arena |
| `--character=swordsman` | pré-seleciona o personagem |
| `--wave=4` | começa na onda N |
| `--godmode` | jogador invulnerável |
| `--noai` | inimigos não atacam |
| `--stats` | overlay de FPS, draw calls, inimigos ativos |

Todas ignoradas em build de release (`OS.IsDebugBuild()`).

## 7. Export

### Presets

| Preset | Alvo | Notas |
|---|---|---|
| `Windows Desktop` | x86_64 | ícone, versão, assinatura opcional |
| `Linux` | x86_64 | — |
| `macOS` | universal | notarização fora do escopo do MVP |

**Web não é um alvo.** Godot 4 com C# não exporta para navegador. Está
documentado como restrição desde a [visão geral](../00-visao-geral.md).

### Configuração

- Export release com `Export With Debug` desligado.
- Build em **`ExportRelease`** (não existe `Release` — ver §8), `Optimize` ligado.
- Excluir `docs/`, `tests/`, `tools/` do pacote (filtro de exclusão no preset).
- Versionamento **SemVer** em `project.godot` (`application/config/version`),
  espelhado no `.csproj`.

### Estrutura de release

```
Contenda-0.1.0-win64.zip
├── Contenda.exe
├── Contenda.pck
├── *.dll (runtime .NET)
├── LICENSE
├── THIRD-PARTY-NOTICES.md
└── README.txt
```

## 8. CI

`.github/workflows/ci.yml`, em todo push e PR:

```
1. setup-dotnet (8.x)
2. dotnet restore && dotnet build -c ExportRelease   ← falha em warning tratado como erro
3. dotnet test tests/Contenda.Tests               ← xUnit, núcleo puro
4. godot --headless --import                      ← valida os assets
5. godot --headless --quit-after 2 --script ...   ← ContentValidator
6. (nightly) godot --headless GdUnit4 runner      ← testes de cena
7. (tag v*) export dos 3 presets + upload do artefato
```

O Godot headless na CI usa a imagem `barichello/godot-ci:4.x-mono` ou download
direto do binário mono — decidido no [M0](../plans/m0-fundacao.md).

## 9. Critérios de aceite

- [ ] Alterar configuração, fechar e reabrir o jogo preserva a escolha.
- [ ] Rebind de `command_confirm` funciona e o HUD reflete a nova tecla.
- [ ] Apagar `settings.cfg` faz o jogo abrir com padrões, sem crash.
- [ ] `--skip-menu --character=gunslinger --wave=4` entra direto no cenário.
- [ ] A build exportada roda numa máquina limpa, sem SDK instalado.
- [ ] O `.zip` contém `THIRD-PARTY-NOTICES.md`.
