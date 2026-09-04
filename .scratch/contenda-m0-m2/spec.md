# Spec: fundação até o combate básico (M0–M2)

A especificação **não vive aqui**. Ela está em [`docs/`](../../docs/README.md),
que é a fonte de verdade. Este arquivo existe só para dar ao
`/to-tickets`, `/implement` e `/code-review` um ponto de entrada no formato que
o tracker local espera.

## Escopo

Os três primeiros milestones do roadmap:

| Milestone | Plano | Entrega |
|---|---|---|
| M0 | [`m0-fundacao.md`](../../docs/plans/m0-fundacao.md) | projeto compilando, CI verde, convenções |
| M1 | [`m1-movimento-e-camera.md`](../../docs/plans/m1-movimento-e-camera.md) | cápsula andando sob a câmera 2.5D travada |
| M2 | [`m2-atributos-e-combate-basico.md`](../../docs/plans/m2-atributos-e-combate-basico.md) | vida, mana, M1 de espada e revólver |

Specs de referência: [01 arquitetura](../../docs/specs/01-arquitetura-tecnica.md) ·
[02 câmera e mundo](../../docs/specs/02-camera-e-mundo-25d.md) ·
[04 atributos](../../docs/specs/04-atributos-vida-mana-stats.md) ·
[07 combate](../../docs/specs/07-combate-armas-e-dano.md)

## Fora deste conjunto

M3 em diante. O [go/no-go do M3](../../docs/plans/m3-comandos-e-habilidades.md)
pode mudar a mecânica central do jogo, e fatiar 60+ tickets antes disso produz
trabalho que envelhece antes de ser feito. Rodar `/to-tickets` de novo por
milestone.

## Regras que valem em todo ticket

Estão em [`CLAUDE.md`](../../CLAUDE.md). As duas que mais reprovam review:
**nenhum nó 2D no gameplay** e **nenhum valor de balanceamento em `.cs`**.

Build: `dotnet build Contenda.sln -c ExportRelease`. Não existe `Release`.

## Fronteira

Um ticket está pronto para começar quando todos os seus bloqueadores estão
concluídos. Hoje: **02 e 03** (o 01 está concluído).
