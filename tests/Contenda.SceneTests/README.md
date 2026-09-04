# Contenda.SceneTests

Testes que precisam de uma árvore de nós viva. **Ainda não executáveis** — o
Godot não está instalado na máquina de desenvolvimento.

## O que vem para cá

Da [spec 15 §1](../../docs/specs/15-qualidade-testes-e-performance.md):

| Alvo | Milestone | Por que não cabe em xUnit |
|---|---|---|
| Movimento relativo à câmera nas 8 direções | M1 | precisa de física e de transformações reais |
| Pulo: coyote time e jump buffer | M1 | depende de `_PhysicsProcess` de verdade |
| Reciclagem de inimigo, 100 ciclos | M5 | o contrato de pool é sobre estado de nó |
| Navegação contornando obstáculo | M5 | precisa de navmesh bakeada |
| Fluxo de telas sem vazar nó | M7 | conta nós na árvore |

O teste de 100 ciclos de reciclagem é o mais importante da lista: vazamento de
handler em nó pooled é o bug que este desenho mais convida
([spec 15 §5](../../docs/specs/15-qualidade-testes-e-performance.md)).

## O que **não** vem para cá

Regra pura — dano, mitigação, cooldown, buffer de comandos, resolução de combo,
progressão de ondas. Isso vive em `Contenda.Tests`, roda em milissegundos e não
precisa de engine. Se um teste desses estiver querendo vir para cá, o desenho
saiu errado: a classe herdou de `Node` quando não devia.
