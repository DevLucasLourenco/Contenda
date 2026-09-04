# 02: Constantes do projeto, cena vazia e arquivos de licença

**What to build:** o jogo passa a **rodar** — abre numa cena vazia sem erro no
console, com os cinco autoloads logando na ordem correta. As camadas de física e
as ações de input ficam nomeadas no projeto e espelhadas em C#, para que nenhum
ticket seguinte precise de string mágica nem de número de camada solto.

**Blocked by:** 01.

**Status:** quase concluído — falta apenas `LICENSE`, que depende de decisão humana

- [x] as 10 camadas de física nomeadas em Project Settings exatamente como na
      [spec 01 §8](../../../docs/specs/01-arquitetura-tecnica.md), espelhadas em
      `src/Core/GameConstants.cs`
- [x] todas as ações de `InputMap` da
      [spec 03 §1](../../../docs/specs/03-input-comandos-e-combos.md) registradas,
      com deadzone 0.2, e `GameConstants.InputActions` expondo cada uma como
      `StringName`
- [x] `GameBootstrap`, `GameSession`, `SceneRouter`, `GameEvents` e
      `AudioDirector` registrados como autoload, nessa ordem, cada um logando o
      nome no `_Ready`
- [x] `ServiceLocator` com acesso tipado aos autoloads
- [x] uma cena vazia definida em `run/main_scene`: rodar o projeto abre sem erro
- [ ] `LICENSE` (**pendente: decisão sua**) e `THIRD-PARTY-NOTICES.md` (feito) na raiz, o segundo no formato da
      [spec 13 §9](../../../docs/specs/13-assets-animacao-e-licencas.md)
- [x] nenhum nó 2D na cena vazia

## Comments

A cena vazia, o `LICENSE` e o `THIRD-PARTY-NOTICES.md` foram absorvidos aqui
porque o eixo Spec do code-review do ticket 01 apontou que não pertenciam a
ticket nenhum, apesar de constarem do M0.

O `LICENSE` exige uma decisão humana sobre qual licença adotar — pergunte antes
de escolher.

---

**Verificado em 2026-09-04**, com o Godot 4.7.2 .NET instalado: rodar o projeto
sobe os cinco autoloads na ordem de dependência correta —
`GameEvents → GameSession → SceneRouter → AudioDirector → GameBootstrap` — e o
`GameBootstrap` imprime nome e versão lidos do assembly e da engine, provando que
a referência ao GodotSharp resolve. Saída limpa, código 0.
