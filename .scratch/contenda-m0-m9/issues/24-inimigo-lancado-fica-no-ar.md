# 24: Inimigo lançado fica no ar e volta a lutar ao cair

**What to build:** o outro lado do combate aéreo. Um inimigo atingido pelo
anti-aéreo para de tentar andar, sobe, pode ser mantido no alto por golpes
aéreos, e ao tocar o chão se recompõe e volta a perseguir.

**Blocked by:** 22, 19

**Status:** done

- [x] Um inimigo lançado **para de tentar andar** enquanto está no ar
- [x] Ele não ataca nem recalcula rota enquanto está no ar
- [x] Ao tocar o chão ele se recompõe e volta a perseguir normalmente
- [x] Golpes aéreos o mantêm no alto, até o limite de acertos
- [x] Passado esse limite, ele cai mesmo que continue apanhando
- [x] Cair não o mata nem o machuca por queda
- [x] O estado aéreo pode ser entrado a partir de perseguição, ataque ou descanso
- [x] Reciclar um inimigo que morreu no ar não o traz de volta flutuando

## Comments

Sem este ticket o anti-aéreo lança um inimigo que continua andando no ar, o que
parece bug mesmo sendo consequência de não haver estado para isso.

O limite de acertos aéreos existe para o modo horda: sem ele, um jogador
habilidoso prende um inimigo no alto indefinidamente enquanto os outros trinta
esperam a vez.

## Implementação

`EnemyState` ganhou `Airborne`. `EnemyStateMachine.RegistrarGolpeRecebido`
passou a receber `lancamentoVertical: bool`: `true` transiciona para
`Airborne` a partir de **qualquer** estado (mesmo já atordoado ou já no ar —
refresca, não empilha); `false` mantém o comportamento antigo (atordoa),
exceto que um golpe comum recebido **já no ar** não derruba para `Staggered`
— é exatamente o combo aéreo do ticket 19 sustentando o inimigo com uma
sequência de acertos horizontais. `Airborne` não tem temporizador: só sai ao
tocar o chão (`Advance(..., estaNoChao: true)`), nunca por relógio — ver os
testes `No_ar_nao_sai_sozinho_por_tempo_nenhum` (600 quadros) e
`Tocar_o_chao_tira_do_ar_e_atordoa`.

Ao tocar o chão, o estado é `Staggered` (não `Chase` direto), seguindo a spec
16 §6 ao pé da letra ("voltando a `Staggered` ao tocar o chão"). `Staggered`
já se recompõe sozinho de volta para `Chase` (`Atordoamento_volta_a_perseguir_sozinho`),
então "volta a perseguir normalmente" do ticket continua satisfeito, só que
com a mesma janela curta de vulnerabilidade de qualquer outro golpe recebido
— ver `Aterrissar_atordoado_ainda_se_recompoe_sozinho`.

**Distinguir lançamento de golpe comum sem campo novo em `DamageInfo`:** o
anti-aéreo (`UppercutBehavior`, Rising Slash) já golpeia com
`Direction = Vector3.Up`; todo golpe comum (inclusive os do combo aéreo, cujo
impulso vertical é aplicado à parte via `ApplyKnockback`, nunca por
`Direction`) tem uma direção majoritariamente horizontal. `EnemyBrain.AoApanhar`
usa o limiar `Direction.Y > 0.7f` — uma folga generosa contra qualquer golpe
só um pouco inclinado para cima continuar contando como comum.

**"Para de andar/atacar/recalcular rota" e "não ataca nem se vira para o
alvo":** `EnemyBrain.MontarIntencao` só chama `NavigationMotor` em `Chase`
(inalterado) e só mira no alvo (`temMira`) fora de `Idle` **e** `Airborne` —
um inimigo no ar não fica se virando para quem acabou de golpeá-lo.

**Bug real encontrado via probe, não só calibração:** `EnemyBrain.Poll()`
lia `Movement.IsGrounded` cru para decidir `estaNoChao`. Esse valor fica
literalmente um quadro atrasado bem no quadro exato de um lançamento: dentro
de `MovementComponent.Tick`, `IsGrounded` é atualizado com `IsOnFloor()` lido
**antes** de somar o recuo deste mesmo quadro (a mesma defasagem que
`AtualizarDash` já documentava, só para o dash). Resultado: lançar um inimigo
**parado no chão** (o caso mais comum) fazia `Airborne` durar um único quadro
— no `Poll` seguinte, `IsGrounded` ainda lia `true` e `Advance` derrubava
direto de volta, antes mesmo do corpo sair do chão de verdade. O probe
original não pegava isso porque seu primeiro teste de lançamento partia de um
inimigo ainda caindo de um teleporte (`IsGrounded` já `false` antes do golpe),
mascarando a corrida. Corrigido com uma property nova em `MovementComponent`,
`IsGroundedConfiavel` (`IsGrounded && Velocity.Y <= 0f`): `Velocity` é lido
**depois** do `MoveAndSlide` do próprio quadro, então já reflete o recuo
aplicado mesmo quando `IsGrounded` ainda não se atualizou. A mesma
defasagem já vivia calculada localmente dentro de `AtualizarDash`
(`realmenteNoAr`), mas só para uso interno do próprio dash — `EnemyBrain`
precisava do mesmo sinal de FORA do componente, daí a property pública em
vez de reimplementar a fórmula num segundo lugar (apontado no code review).

"Cair não o mata nem o machuca por queda": trivialmente satisfeito — o
projeto não tem dano de queda nenhum.

"Golpes aéreos o mantêm no alto, até o limite de acertos" / "passado esse
limite, ele cai mesmo que continue apanhando": a máquina de estado deste
ticket não sabe nada sobre CONTAGEM de acertos — só sobre contato com o
chão. Quem decide se um novo acerto ainda ganha impulso vertical é
`AerialJuggleState.RegistrarAcerto` (ticket 19, já coberto por
`AerialJuggleStateTests.cs`); uma vez que o limite é atingido e o atacante
para de aplicar `ApplyKnockback`, a gravidade sozinha desce o inimigo e
`Advance` detecta `estaNoChao` normalmente — o mesmo caminho que
`TickAterrissagem` do probe já exercita (nenhum impulso novo depois do
lançamento inicial, ainda assim aterrissa). Testar o limite de novo aqui
seria duplicar a cobertura de `AerialJuggleStateTests.cs` sem exercitar
nada específico deste ticket.

**Probe novo:** `src/Tools/AerialEnemyProbe.cs` +
`scenes/debug/AerialEnemyProbe.tscn`, um arquivo dedicado em vez de estender
o `EnemyBrainProbe.cs` do ticket 22 — lançamento vertical de verdade (dano +
`ApplyKnockback`, a mesma assinatura do anti-aéreo real) na árvore de nós,
não só a máquina de estado isolada. Quatro fases: lançamento (confirma
`Airborne` + fisicamente no ar), sustentado (golpe comum no meio do voo não
derruba, sem perseguir nem atacar, sem sair por tempo), aterrissagem (volta a
tocar o chão e sai de `Airborne`), e reset no meio do ar (`ResetForSpawn`
durante o voo não deixa ninguém preso flutuando — checado no mesmo quadro do
reset, antes de qualquer `Poll` rodar de novo, já que o jogador fica bem ao
lado de propósito e reage a partir de `Idle` tão rápido quanto qualquer outro
quadro).

### Code review

Duas revisões em paralelo (Standards e Spec) sobre o diff staged. Achados
reais, ambos corrigidos:

- **Spec + Standards, convergindo no mesmo ponto:** aterrissar levava direto
  para `Chase`, mas a spec 16 §6 é normativa ("voltando a `Staggered` ao
  tocar o chão"), não só sugestão em prosa. Corrigido: `Advance` agora
  transiciona `Airborne → Staggered` ao tocar o chão; como `Staggered` já se
  recompõe sozinho para `Chase`, "volta a perseguir normalmente" do ticket
  continua satisfeito, com o bônus de restaurar a janela de vulnerabilidade
  que a spec pedia. Teste renomeado/ajustado
  (`Tocar_o_chao_tira_do_ar_e_atordoa`) e um novo cobrindo a recomposição
  automática (`Aterrissar_atordoado_ainda_se_recompoe_sozinho`).
- **Standards, Feature Envy/Duplicated Code:** `EnemyBrain.Poll` reimplementava
  por fora a mesma fórmula que `MovementComponent.AtualizarDash` já calculava
  por dentro (`realmenteNoAr`) para a mesma defasagem de `IsGrounded`.
  Corrigido: nova property pública `MovementComponent.IsGroundedConfiavel`,
  consumida por `EnemyBrain` em vez de duplicar `IsGrounded && Velocity.Y`
  ali mesmo.
- **Spec, cobertura:** o limite de juggle aéreo ("passado esse limite, ele
  cai mesmo que continue apanhando") não tinha um teste dedicado sob a ótica
  deste ticket. Avaliado e decidido não duplicar: o limite em si já é
  coberto por `AerialJuggleStateTests.cs` (ticket 19), e a única coisa que
  este ticket precisa garantir — a máquina de estado não olha para contagem
  de acertos, só para contato com o chão — já está coberta por
  `TickAterrissagem` do probe (aterrissa mesmo sem nenhum impulso novo desde
  o lançamento inicial). Documentado explicitamente no texto acima em vez de
  adicionar um teste redundante.
- **Standards, Primitive Obsession / Divergent Change (julgamento, sem
  mudança):** o limiar `Direction.Y > 0.7f` e o fato de `EnemyBrain.Poll`
  mexer em duas coisas no mesmo commit (fiar o `Airborne` + corrigir o bug de
  `IsGrounded`) foram sinalizados como aceitáveis pelos próprios revisores —
  o primeiro por ser ponto único de uso e evitar campo novo em `DamageInfo`
  por decisão explícita do ticket; o segundo porque o bug só aparece através
  da feature nova. Nenhuma mudança feita.

Gate completo re-executado após as correções: 317/317 testes, 12/12 probes
headless (incluindo `AerialEnemyProbe` 3x seguidas), `ExportRelease` e
`check-no-2d.sh` limpos.
