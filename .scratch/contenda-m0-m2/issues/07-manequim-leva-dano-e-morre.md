# 07: Manequim leva dano com mitigação e morre uma vez só

**What to build:** existe dano no jogo. Um manequim de treino tem vida, recebe
golpes por hitbox, sofre mitigação por defesa e morre — uma única vez, mesmo
levando dois golpes no mesmo frame.

**Blocked by:** 05.

**Status:** ready-for-agent

- [ ] `StatBlock` como POCO, com modificadores por fonte e ordem de aplicação
      `Flat → PercentAdd → PercentMult`
      ([spec 04 §1](../../../docs/specs/04-atributos-vida-mana-stats.md)),
      com cache por stat
- [ ] `StatsComponent` como casca fina sobre o `StatBlock`, com
      `RemoveBySource` — é o que torna a reversão de transformação trivial no M4
- [ ] `HealthState` (POCO) + `HealthComponent` (`Node`, `IDamageable`), com
      **fila de dano resolvida num ponto único do frame**
- [ ] morte idempotente: dois golpes no mesmo frame disparam `Died` **uma** vez
- [ ] i-frames de 0.25 s no jogador; `ResetForSpawn()` em ambos os componentes
- [ ] `DamageInfo`, `Team`, hitbox e hurtbox nas camadas corretas, com checagem
      de time **também em código**, não só por camada de física
- [ ] `TrainingDummy.tscn` com vida e barra, três deles na arena, um revidando
- [ ] testes xUnit: mitigação, morte idempotente, e adicionar/remover modificador
      devolve o stat ao valor base **exato**

## Comments

Este é o ticket que decide se o M4 vai funcionar. Se a reversão de stats não for
exata aqui, "reverter transformação" vira fonte permanente de bug — ver
[ADR-006](../../../docs/plans/riscos-e-decisoes.md).
