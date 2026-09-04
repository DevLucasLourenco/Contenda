# 08: M1 com espada encadeia três golpes

**What to build:** o botão M1 vira um ataque de verdade. Clicar três vezes no
ritmo encadeia Slash 1 → 2 → 3, com dano crescente e avanço a cada golpe; perder
a janela reinicia o combo.

**Blocked by:** 07.

**Status:** ready-for-agent

- [ ] `IWeapon`, `WeaponBase` e `WeaponDefinition`, com a arma instanciada num
      `WeaponSocket` — M1 delega à arma, **sem nenhum `if` sobre o personagem**
- [ ] `MeleeComboStep` como `Resource`, com janela de hit e janela de combo por
      passo ([spec 07 §3](../../../docs/specs/07-combate-armas-e-dano.md))
- [ ] `MeleeWeapon` com hitbox `Area3D` **habilitada só entre
      `HitWindowStart` e `HitWindowEnd`** — nunca ligada permanentemente
- [ ] `SwordWeapon` com a cadeia de 3 e avanço `ForwardStep` na direção de mira
- [ ] cada golpe atinge cada alvo **uma única vez** (lista de `InstanceId`
      limpa a cada passo)
- [ ] `CombatComponent` com `ActionLock` **por fonte e com duração**, somado por
      OR — duas fontes travando o movimento, uma liberando, não destrava
- [ ] `data/weapons/sword.tres` e os três `MeleeComboStep` em `.tres`
- [ ] nenhum valor de dano hardcoded em `.cs`

## Comments

As janelas de hit vêm de `MeleeComboStep`, **não** de call-method track na
animação: trocar o modelo no M8 troca as animações, e tracks embutidos se
perderiam junto — ver [spec 13 §6](../../../docs/specs/13-assets-animacao-e-licencas.md).
