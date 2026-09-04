# 10: Mana com consumo atômico e regeneração atrasada

**What to build:** o recurso que sustenta habilidades e transformações. Gastar
mana pausa a regeneração por um segundo; tentar gastar sem saldo não altera nada;
e existe um dreno parcial, que é o que as transformações vão usar no M4.

**Blocked by:** 07.

**Status:** ready-for-agent

- [ ] `ManaDefinition` como `Resource`, com `mana_swordsman.tres` e
      `mana_gunslinger.tres`
- [ ] `ManaComponent` com `MaxMana` lido do `StatBlock`
- [ ] **`TryConsume` é tudo-ou-nada:** sem saldo, retorna `false` e não altera
      `CurrentMana` — é o método que as habilidades usarão
- [ ] **`Drain` é parcial:** consome o que houver e dispara `Depleted` ao zerar —
      é o método que as transformações usarão, porque o dreno não pode "falhar"
- [ ] regeneração com delay de 1.0 s após qualquer gasto
- [ ] `ResetForSpawn()`
- [ ] testes xUnit: `TryConsume` insuficiente não altera nada; o delay de
      regeneração aplica e depois libera

## Comments

Roda em paralelo com o 08 e o 09 — depende só do 07. Dois trilhos independentes
abrem aqui.

A distinção `TryConsume` vs `Drain` parece pedante agora e é a coisa que faz o M4
funcionar: uma transformação que "falha" ao drenar nunca reverteria — ver
[spec 04 §3](../../../docs/specs/04-atributos-vida-mana-stats.md).
