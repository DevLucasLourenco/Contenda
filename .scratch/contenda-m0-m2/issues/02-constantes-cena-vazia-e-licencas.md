# 02: Constantes do projeto, cena vazia e arquivos de licença

**What to build:** o jogo passa a **rodar** — abre numa cena vazia sem erro no
console, com os cinco autoloads logando na ordem correta. As camadas de física e
as ações de input ficam nomeadas no projeto e espelhadas em C#, para que nenhum
ticket seguinte precise de string mágica nem de número de camada solto.

**Blocked by:** 01.

**Status:** ready-for-agent

- [ ] as 10 camadas de física nomeadas em Project Settings exatamente como na
      [spec 01 §8](../../../docs/specs/01-arquitetura-tecnica.md), espelhadas em
      `src/Core/GameConstants.cs`
- [ ] todas as ações de `InputMap` da
      [spec 03 §1](../../../docs/specs/03-input-comandos-e-combos.md) registradas,
      com deadzone 0.2, e `GameConstants.InputActions` expondo cada uma como
      `StringName`
- [ ] `GameBootstrap`, `GameSession`, `SceneRouter`, `GameEvents` e
      `AudioDirector` registrados como autoload, nessa ordem, cada um logando o
      nome no `_Ready`
- [ ] `ServiceLocator` com acesso tipado aos autoloads
- [ ] uma cena vazia definida em `run/main_scene`: rodar o projeto abre sem erro
- [ ] `LICENSE` e `THIRD-PARTY-NOTICES.md` na raiz, o segundo no formato da
      [spec 13 §9](../../../docs/specs/13-assets-animacao-e-licencas.md)
- [ ] nenhum nó 2D na cena vazia

## Comments

A cena vazia, o `LICENSE` e o `THIRD-PARTY-NOTICES.md` foram absorvidos aqui
porque o eixo Spec do code-review do ticket 01 apontou que não pertenciam a
ticket nenhum, apesar de constarem do M0.

O `LICENSE` exige uma decisão humana sobre qual licença adotar — pergunte antes
de escolher.
