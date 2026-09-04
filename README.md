# Contenda

Jogo de ação/luta **2.5D** feito em **Godot 4.7.x .NET (C#)**: mundo, personagens,
inimigos e colisões 100% 3D, com a câmera travada em um ângulo superior inclinado.
O "2.5D" vem da **apresentação e das restrições de câmera/controle** — nunca de
sprites ou nós 2D.

> **Nome:** a conversa de concepção usava o codinome *Infinity Wars*. Este
> repositório adota **Contenda** como nome do projeto e raiz de namespace
> (`Contenda.*`). Trocar o nome depois é uma alteração de namespace + `.csproj`.

## Pilares

1. **É 3D de verdade.** `CharacterBody3D`, `NavigationRegion3D`, `Camera3D`.
   Nenhum nó 2D no gameplay.
2. **Composição, não herança.** Personagem é um contêiner de componentes.
   Nada de `Player.cs` de 4.000 linhas nem `if (personagem == swordsman)`.
3. **Data-driven.** Habilidades, transformações, personagens, inimigos e ondas
   são `Resource` (`.tres`). Adicionar conteúdo não altera código.
4. **Sem GDScript.** C# em 100% da lógica.

## Documentação

Comece por [docs/README.md](docs/README.md).

| | |
|---|---|
| Visão geral e escopo | [docs/00-visao-geral.md](docs/00-visao-geral.md) |
| Especificações | [docs/specs/](docs/specs/) |
| Roadmap e milestones | [docs/plans/roadmap.md](docs/plans/roadmap.md) |
| Decisões e riscos | [docs/plans/riscos-e-decisoes.md](docs/plans/riscos-e-decisoes.md) |

## Status

Fase de especificação. Nenhum código ainda — a implementação começa pelo
[Milestone 0](docs/plans/m0-fundacao.md).
