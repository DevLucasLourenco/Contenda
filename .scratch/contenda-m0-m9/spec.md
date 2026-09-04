# Spec: Contenda, da fundação à build 0.1

A especificação **não vive aqui**. Ela está em [`docs/`](../../docs/README.md),
que é a fonte de verdade. Este arquivo dá ao `/to-tickets`, `/implement` e
`/code-review` um ponto de entrada no formato que o tracker local espera.

## Escopo

Os dez milestones do [roadmap](../../docs/plans/roadmap.md), M0 a M9, mais a
reformulação de mobilidade e combate pedida depois da concepção original:
pulo, dash, golpes críticos, combate aéreo, transformações que trocam a arma, e
a arena urbana em três níveis
([spec 16](../../docs/specs/16-mobilidade-criticos-e-combate-aereo.md) e
[spec 17](../../docs/specs/17-arena-urbana.md)).

## Estado

| Ticket | Estado |
|---|---|
| 01 — projeto compilando | **concluído** (`3f5c3bd`) |
| 02 — constantes, cena vazia, licenças | **concluído**, exceto `LICENSE` (decisão humana) |
| 03 — testes, CI e regras de estilo | **concluído** (`a9fd088`) |
| 04 em diante | pendentes |

**Fronteira hoje:** 04 e 05 — e ambos exigem o **Godot 4.7.x .NET instalado**,
que não está. Criar cenas, bakear navegação e validar o `project.godot`
(escrito inteiramente à mão) não são possíveis sem o editor.

## Mapa dos tickets

| Faixa | Assunto |
|---|---|
| 01–03 | fundação: projeto, constantes, testes e CI |
| 04–06 | arena de bloqueio, câmera 2.5D, movimento, trava anti-2D |
| 07–12 | vida, mana, atributos, armas, peso de impacto, HUD provisório |
| 13–16 | buffer de comandos, habilidades por sequência, guia de combos |
| 17–21 | pulo e dash, críticos, combate aéreo, formas que trocam a arma, cidade |
| 22–26 | inimigos: percepção, navegação, estado aéreo, reciclagem, vida visível |
| 27–29 | modo horda: ondas, chefe, placar e resultado |
| 30–33 | menus, seleção, pause, configurações e HUD final — **fecha o MVP 0.1** |
| 34–36 | arte, animação, efeitos e áudio |
| 37–39 | balanceamento, desempenho e builds |

A numeração **não é cronológica**: 17–21 nasceram depois e pertencem, por
dependência, ao meio do conjunto. A linha `Blocked by` de cada ticket é a
verdade sobre a ordem.

## Regras que valem em todo ticket

Estão em [`CLAUDE.md`](../../CLAUDE.md). As duas que mais reprovam review:
**nenhum nó 2D no gameplay** e **nenhum valor de balanceamento em `.cs`**.

Build: `dotnet build Contenda.sln -c ExportRelease`. Não existe `Release`.
Testes: `dotnet test Contenda.sln -c ExportRelease`.

## Portões de decisão

Três pontos em que o resultado pode mudar o plano, e não só o cronograma:

| Depois do ticket | Pergunta | Se a resposta for não |
|---|---|---|
| 05 | a câmera 2.5D é legível e agradável? | ajustar ângulo, distância e FOV **antes** de seguir |
| 14 | executar habilidade por sequência é divertido? | plano B: teclas numéricas disparam, sequência vira bônus |
| 23 | quarenta inimigos rodam a 60 quadros? | baixar o teto para 25 e redesenhar as ondas |

O portão do 14 é o mais importante do projeto: é onde se descobre se a ideia
central funciona, com o custo de mudar ainda baixo.
