# Contenda

Jogo de ação 2.5D em arena, **Godot 4.7.x .NET + C#**. Mundo, colisão, navegação
e render são 3D; o "2.5D" vem da câmera travada num ângulo superior.

A especificação completa está em [`docs/`](docs/README.md). Comece pela
[visão geral](docs/00-visao-geral.md) e pelo [roadmap](docs/plans/roadmap.md).

## Regras não negociáveis

1. **Nada de nó 2D no gameplay.** `Node2D`, `CharacterBody2D`, `Camera2D`,
   `Sprite2D`, `Area2D`, `TileMap` são reprovação de review. `Control` e
   `CanvasLayer` só para UI. Ver [spec 02](docs/specs/02-camera-e-mundo-25d.md).
2. **Nada de GDScript.** 100 % C#.
3. **Composição, não herança.** O personagem é um contêiner de componentes;
   jogador e inimigo usam os mesmos. Nada de `Player.cs` gigante nem de
   `if (personagem == swordsman)`. Ver [spec 01](docs/specs/01-arquitetura-tecnica.md).
4. **Balanceamento vive em `.tres`, nunca em `.cs`.** Nova habilidade,
   transformação, inimigo ou onda = novo `Resource`. Ver
   [spec 12](docs/specs/12-dados-resources-e-conteudo.md).
5. **A câmera nunca é filha do jogador.** `CameraRig` é irmão e recebe só
   posição, jamais rotação.

## Build

As configurações do `Godot.NET.Sdk` são **`Debug`, `ExportDebug` e
`ExportRelease`**. **Não existe `Release`.**

```bash
dotnet build Contenda.sln -c ExportRelease
```

O `.sln` declara as três; um `-c Release` equivocado falha alto em vez de
compilar Debug em silêncio. Ao regerar a solução use
`dotnet new sln --format sln` — o SDK .NET 10 gera `.slnx` por padrão, que o
editor Godot não lê.

Convenções de código em
[`docs/plans/convencoes-de-codigo.md`](docs/plans/convencoes-de-codigo.md);
portões de qualidade por PR em
[`docs/specs/15-qualidade-testes-e-performance.md`](docs/specs/15-qualidade-testes-e-performance.md).

## Agent skills

### Issue tracker

Issues e specs vivem como markdown local em `.scratch/<feature-slug>/`; não há
remote git neste repositório. See `docs/agents/issue-tracker.md`.

### Triage labels

Vocabulário canônico, sem renomeações: `needs-triage`, `needs-info`,
`ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: um `CONTEXT.md` na raiz e ADRs em `docs/adr/` — nenhum dos dois
existe ainda, e é para criá-los sob demanda, não de antemão. Note que as
decisões de arquitetura já tomadas estão registradas como ADRs em prosa em
[`docs/plans/riscos-e-decisoes.md`](docs/plans/riscos-e-decisoes.md).
See `docs/agents/domain.md`.
