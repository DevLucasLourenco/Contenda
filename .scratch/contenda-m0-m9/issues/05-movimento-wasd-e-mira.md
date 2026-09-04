# 05: Personagem anda com WASD relativo à câmera, encarando o mouse

**What to build:** o jogo fica jogável pela primeira vez. Uma cápsula anda pela
arena com WASD, o movimento é relativo à **tela** e não ao mundo, o personagem
encara o cursor, e a câmera acompanha sem nunca girar.

**Blocked by:** 04.

**Status:** ready-for-agent

- [ ] `CharacterController` com o ciclo de duas passadas `Bind` → `Configure`,
      mais `CharacterContext` e `ICharacterComponent`
      ([spec 01 §4](../../../docs/specs/01-arquitetura-tecnica.md))
- [ ] `IntentFrame` e `PlayerInputController`: o input é lido em
      `_PhysicsProcess` e vira intenção; nenhum componente lê `Input` direto
- [ ] `MovementComponent` com aceleração e desaceleração, gravidade e
      `MoveAndSlide` — **consome `IntentFrame`**, para que a IA reuse o mesmo
      componente no M5
- [ ] **`W` move para cima na tela em qualquer ponto da arena**; as 8 direções
      testadas
- [ ] `TargetingComponent` projetando o mouse no plano na altura do torso
- [ ] follow com `SmoothDamp` (não `Lerp` por delta), com dead zone
- [ ] girar o personagem 360° não altera um pixel do enquadramento
- [ ] subir na plataforma elevada lê como altura
- [ ] nenhum componente usa `GetNode("../…")`

## Comments

O `FaceMode` padrão é `Aim` (encara o cursor), com `Movement` disponível para
comparar — ver [ADR-011](../../../docs/plans/riscos-e-decisoes.md).

Fim deste ticket é o **go/no-go do M1**: a câmera 2.5D é legível e agradável de
jogar? Refazer a câmera depois do M5 é dez vezes mais caro.
