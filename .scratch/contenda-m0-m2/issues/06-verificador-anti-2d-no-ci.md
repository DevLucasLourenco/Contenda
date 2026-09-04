# 06: Verificador anti-2D no CI

**What to build:** a regra número um do projeto passa a ser aplicada por máquina
em vez de por disciplina. Introduzir um nó 2D numa cena de gameplay reprova o
pull request automaticamente.

**Blocked by:** 03, 04.

**Status:** ready-for-agent

- [ ] script em `tools/` que falha se `Node2D`, `CharacterBody2D`, `Camera2D`,
      `Sprite2D`, `Area2D`, `CollisionShape2D`, `TileMap` ou `AnimatedSprite2D`
      aparecerem em `scenes/` fora de `scenes/ui/`
- [ ] step no `ci.yml` executando o script, bloqueante
- [ ] **teste do teste:** adicionar um `Sprite2D` à arena reprova o CI, e removê-lo
      volta ao verde
- [ ] mensagem de erro cita o arquivo, a linha e o link para a
      [spec 02](../../../docs/specs/02-camera-e-mundo-25d.md)

## Comments

Ticket pequeno e deliberadamente cedo. Não é fatia vertical — é a trava que
protege o projeto inteiro, e o histórico do projeto anterior é a justificativa:
sem ela, a deriva para 2D depende de alguém lembrar em cada review.

`Control` e `CanvasLayer` são permitidos e não devem ser sinalizados; a UI é 2D
por natureza.
