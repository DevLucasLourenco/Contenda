# 10: Mana com consumo atômico e regeneração atrasada

**What to build:** o personagem tem mana. Gastar mana desconta o valor e
**pausa** a regeneração por um instante, para que gastar tenha custo de verdade;
depois ela volta a subir sozinha. Tentar gastar sem saldo não gasta nada e não
deixa o recurso num estado intermediário.

**Blocked by:** 07

**Status:** CONCLUÍDO

- [x] Gastar mana desconta o valor e pausa a regeneração por cerca de um segundo
- [x] Passada a pausa, a mana volta a subir até o máximo
- [x] Tentar gastar sem saldo suficiente **não altera nada**
- [x] Existe um consumo parcial, que leva o que houver e avisa ao zerar
- [x] Existe um jeito de devolver a mana ao estado de recém-criado
- [x] Testes cobrindo consumo atômico, consumo parcial e o atraso da regeneração

## Comments

Duas formas de gastar, e a distinção **não** é detalhe de implementação:

- **Atômico** (tudo ou nada) — usado por habilidades no M3. Se não há mana, a
  habilidade simplesmente não sai, e nada é alterado.
- **Parcial** (leva o que houver e avisa ao zerar) — usado por transformações no
  M4. O dreno contínuo não pode "falhar": ele esgota e força a reversão.

Escolher errado aqui aparece só no M4, como uma transformação que não reverte.

Independente dos tickets 08 e 09: só precisa da base do ticket 07. É o segundo
trilho, se houver duas pessoas.

---

**Concluído em 2026-09-05.** [`ManaState`](../../../src/Components/Mana/ManaState.cs)
é o `HealthState` da mana: POCO, sem nó envolvido, com
[`ManaComponent`](../../../src/Components/Mana/ManaComponent.cs) como casca
fina por cima — mesmo desenho, mesmo motivo (spec 15 §1: a lógica que precisa
de teste não pode herdar de `Node`).

**O que foi construído:**

- `TryConsume` (atômico) e `Drain` (parcial) são dois métodos diferentes desde
  o início, como os comentários do ticket pediam — não uma flag num método só.
  `TryConsume` recusa e não toca em nada sem saldo; `Drain` nunca falha, leva
  o que houver e dispara `Depleted` ao zerar.
- A pausa de regeneração é por fonte única (não por gasto): cada novo consumo
  REINICIA a pausa em vez de somar, e `Advance` só volta a regenerar quando ela
  zera. A suíte `ManaStateTests` (21 casos) cobre isso, inclusive o caso que
  motivou a distinção atômico/parcial: gastar de novo perto do fim da pausa
  não deixa a regeneração vazar um instante antes da hora.
- `ManaRegen` do `StatBlock` é lido a cada `Tick`, nunca fixado — mesma escolha
  do `AttackInterval` do revólver no ticket 09. Uma transformação que altera a
  regeneração no M4 não precisa editar `ManaDefinition`.
- `MaxMana` reage a `StatChanged` exatamente como `MaxHealth` já reagia em
  `HealthComponent`: um modificador no `StatBlock` chega ao `ManaState` sozinho,
  preservando a FRAÇÃO atual (a mesma razão do `HealthState.SetMax` — sem
  isso, uma transformação que aumenta o teto encheria ou esvaziaria de graça).
- `CharacterContext.Mana`, `CharacterDefinition.Mana` e o `ManaComponent` no
  `Character.tscn` compartilhado — Swordsman (100 mana, regen 5/s) e Gunslinger
  (120, regen 7/s) usam o mesmo componente, valores da spec 04 §5, só o `.tres`
  muda.
- `CharacterController` chama `ManaComponent.Tick` a cada quadro de física,
  entre o combate e a resolução de dano — nenhuma outra leitura de mana existe
  ainda neste milestone, então a posição exata na ordem só importa a partir do
  M3 (habilidades consumindo mana).

**Verificado por [`ManaProbe`](../../../src/Tools/ManaProbe.cs)**, na árvore
real, headless: `MaxMana`/`Current` carregando do `swordsman.tres`, consumo
atômico com e sem saldo, `Drain` zerando e disparando `Depleted`, um
modificador de `MaxMana` no `StatBlock` refletindo no `ManaState` sem nenhuma
ligação manual, remover o modificador devolvendo o valor base, e
`ResetForSpawn` enchendo a mana de novo. As verificações de tempo são
propositalmente folgadas — a precisão da pausa de regeneração já está coberta
com relógio exato no xUnit; o que o probe prova é só a fiação (o
`CharacterController` chamando `Tick` sozinho, o `.tres` carregando, o
`StatBlock` propagando).

**Não verificado nesta sessão:** sensação de "gastar mana custa de verdade"
em playtest manual — não há UI de mana ainda (ticket 12) para observar a barra
pausar e voltar a subir.
