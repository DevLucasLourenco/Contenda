# 09: M1 com revólver dispara, gasta munição e recarrega

**What to build:** a prova de que a arquitetura data-driven funciona. Trocar o
personagem por tecla de debug muda o que M1 faz — de combo de espada para tiro
hitscan — **sem uma linha de código condicional sobre qual personagem é**.

**Blocked by:** 08.

**Status:** ready-for-agent

- [ ] `HitscanWeapon` com raycast a partir do cano na direção de mira,
      dispersão dentro de `SpreadDegrees` e tracer até o ponto de impacto
- [ ] `RevolverWeapon` com cilindro de 6 e recarga automática de 1.6 s ao esvaziar
- [ ] `AttackSpeed` do `StatBlock` dividindo o `AttackInterval` — é assim que
      Overdrive dará +50 % de cadência no M4 sem tocar na arma
- [ ] `CharacterDefinition` como `Resource`, com `swordsman.tres` e
      `gunslinger.tres` usando os valores da
      [spec 08 §6](../../../docs/specs/08-personagens.md)
- [ ] `data/weapons/revolver.tres`
- [ ] tecla de debug alterna o personagem em runtime
- [ ] **uma única cena de personagem** serve os dois; não existem
      `Swordsman.tscn` nem `Gunslinger.tscn`

## Comments

O critério que importa aqui não é o tiro funcionar — é a ausência de ramificação.
Se aparecer um `if (id == "gunslinger")` em qualquer lugar do código de gameplay,
o ticket falhou, ainda que o jogo funcione.
