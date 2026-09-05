# 05: Personagem anda com WASD relativo à câmera, encarando o mouse

**What to build:** o jogo fica jogável pela primeira vez. Uma cápsula anda pela
arena com WASD, o movimento é relativo à **tela** e não ao mundo, o personagem
encara o cursor, e a câmera acompanha sem nunca girar.

**Blocked by:** 04.

**Status:** implementado — três critérios dependem de teste com teclado

- [x] `CharacterController` com o ciclo de duas passadas `Bind` → `Configure`,
      mais `CharacterContext` e `ICharacterComponent`
      ([spec 01 §4](../../../docs/specs/01-arquitetura-tecnica.md))
- [x] `IntentFrame` e `PlayerInputController`: o input é lido em
      `_PhysicsProcess` e vira intenção; nenhum componente lê `Input` direto
- [x] `MovementComponent` com aceleração e desaceleração, gravidade e
      `MoveAndSlide` — **consome `IntentFrame`**, para que a IA reuse o mesmo
      componente no M5
- [ ] **`W` move para cima na tela em qualquer ponto da arena**; as 8 direções
      testadas
- [x] `TargetingComponent` projetando o mouse no plano na altura do torso
- [x] follow com `SmoothDamp` (não `Lerp` por delta), com dead zone
- [ ] girar o personagem 360° não altera um pixel do enquadramento
- [ ] subir na plataforma elevada lê como altura
- [x] nenhum componente usa `GetNode("../…")`

## Comments

O `FaceMode` padrão é `Aim` (encara o cursor), com `Movement` disponível para
comparar — ver [ADR-011](../../../docs/plans/riscos-e-decisoes.md).

Fim deste ticket é o **go/no-go do M1**: a câmera 2.5D é legível e agradável de
jogar? Refazer a câmera depois do M5 é dez vezes mais caro.

---

**Implementado em 2026-09-04.** 40 testes verdes, build e verificador anti-2D
limpos, o jogo sobe sem erro nem aviso com o personagem instanciado na arena e a
câmera seguindo-o.

**Três critérios ficam abertos, e é honesto dizer por quê:** não há teclado em
modo headless. Estão codificados e cobertos pela matemática, mas ninguém apertou
uma tecla ainda.

- **"W move para cima na tela; as 8 direções testadas"** — a conversão está
  coberta por 10 testes, incluindo a rosa completa das 8 direções e as diagonais
  exatamente entre os eixos. Falta sentir no teclado.
- **"girar 360° não altera o enquadramento"** — garantido por construção: o
  `CameraRig` é irmão do jogador e só recebe posição. Falta confirmar jogando.
- **"subir na plataforma lê como altura"** — a rampa conecta na navegação
  (verificado no ticket 04), mas a leitura visual é julgamento humano.

Rode `tools/abrir-editor.cmd` e jogue: é o mesmo momento do **go/no-go do M1**.

**Achados do code-review corrigidos:**

- `TargetingComponent` e `PlayerInputController` buscavam o viewport a cada
  quadro — lookup de nó em caminho crítico, proibido pelas convenções §5. Agora
  cacheado no `Bind`.
- A gravidade lia `corpo.Velocity.Y` depois de `Accelerate` já ter devolvido um
  vetor com Y próprio. Iguais hoje, mas o pulo do ticket 17 faria a gravidade
  integrar um valor velho **em silêncio**.
- `CharacterDefinition` ausente caía em valores padrão sem avisar; agora falha
  alto e desliga a física.
- O giro da câmera era cacheado no `Configure`, o que quebrava a edição do
  `.tres` em runtime que o `CameraSettings` promete.
- `PlayerInputController` atualizava a mira dentro do `Poll`, escondendo um
  componente dentro de outro; a ordem do quadro passou para o contêiner, como a
  spec 01 §6 manda.
- `ColetarComponentes` atravessava um `CharacterController` aninhado e roubaria
  os componentes dele.

**Dívida registrada:** o `switch` de registro de componentes vira ponto de edição
repetida junto com o `CharacterContext` conforme os componentes crescem. Trocar
por auto-registro quando passar de meia dúzia — anotado no próprio código.
