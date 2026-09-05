# Contenda

Contenda é um jogo de **ação em arena, 2.5D**, feito em **Godot 4.7.x .NET
(C#)**: o jogador escolhe um entre dois arquétipos e sobrevive a ondas
crescentes de inimigos numa arena 3D, executando habilidades por **sequências
de movimento** (WASD) em vez de teclas de atalho — a mecânica que o projeto
existe para provar.

Mundo, personagens, inimigos e colisão são **100% 3D**; o "2.5D" vem só da
apresentação — uma `Camera3D` travada num ângulo superior, que segue o jogador
e nunca gira. Referências de tom: *V Rising*, *Battlerite*, *Darksiders
Genesis* — não *Dragon Ball FighterZ*: não há plano de luta 2D nem sprites.

> **Nome:** a concepção usava o codinome *Infinity Wars*; o repositório e o
> namespace (`Contenda.*`) já adotam **Contenda**.

## Os dois arquétipos

| | Arma | M1 (ataque básico) faz |
|---|---|---|
| **Swordsman** | Espada | combo corpo a corpo de três golpes |
| **Gunslinger** | Revólver | tiro instantâneo (hitscan), munição e recarga |

O mesmo botão, a mesma `CharacterController`, o mesmo `CombatComponent` — o
que muda é qual `Resource` (`.tres`) está equipado. Trocar de personagem nunca
passa por um `if` no código; é a regra não negociável nº 3 abaixo.

## Pilares

1. **É 3D de verdade.** `CharacterBody3D`, `NavigationRegion3D`, `Camera3D`.
   Nenhum nó 2D no gameplay — só `Control`/`CanvasLayer` na UI.
2. **Composição, não herança.** Personagem é um contêiner de componentes
   (`HealthComponent`, `CombatComponent`, `MovementComponent`, …). Jogador e
   inimigo usam os mesmos. Nada de `Player.cs` de 4.000 linhas nem de
   `if (personagem == swordsman)`.
3. **Data-driven.** Habilidades, armas, transformações, personagens, inimigos
   e ondas são `Resource` (`.tres`). Adicionar ou balancear conteúdo não toca
   em código C#.
4. **Sem GDScript.** 100% C#, inclusive tooling e scripts de editor.
5. **Núcleo testável fora da engine.** Regras puras — combo, dano, travas de
   ação, cadência de tiro — são POCOs com testes xUnit, sem abrir o Godot.

## Desenvolvimento

Requer a edição **.NET** do Godot 4.7 (a padrão não roda C#). Abra o projeto
com o atalho `Contenda (Godot .NET)` ou `tools/abrir-editor.cmd` — nunca com a
edição sem "mono" no nome, que remove a configuração de C# do
`project.godot`. Build de linha de comando:

```bash
dotnet build Contenda.sln -c ExportRelease
```

Convenções de código, portões de qualidade por PR e as regras de ferramentas
desta máquina estão em [`docs/plans/convencoes-de-codigo.md`](docs/plans/convencoes-de-codigo.md).

## Documentação

Comece por [docs/README.md](docs/README.md).

| | |
|---|---|
| Visão geral e escopo | [docs/00-visao-geral.md](docs/00-visao-geral.md) |
| Especificações | [docs/specs/](docs/specs/) |
| Roadmap e milestones | [docs/plans/roadmap.md](docs/plans/roadmap.md) |
| Decisões e riscos | [docs/plans/riscos-e-decisoes.md](docs/plans/riscos-e-decisoes.md) |

## Status

Em desenvolvimento ativo. **M0** (fundação) e **M1** (movimento e câmera
2.5D) estão fechados; **M2** (atributos e combate básico) está em andamento —
vida, dano e o combo de espada já funcionam, o revólver do Gunslinger é o
ticket corrente. Progresso detalhado, por ticket, em
[docs/plans/roadmap.md](docs/plans/roadmap.md) e em
`.scratch/contenda-m0-m9/issues/`.
