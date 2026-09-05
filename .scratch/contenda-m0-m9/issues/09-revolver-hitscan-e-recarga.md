# 09: Ataque básico com revólver, munição e recarga

**What to build:** trocando para a pistoleira, o **mesmo** botão esquerdo passa
a atirar em vez de golpear. O tiro acerta onde o cursor aponta, gasta munição do
tambor e recarrega sozinho quando esvazia. Segurar o botão mantém a cadência.

**Blocked by:** 08

**Status:** CONCLUÍDO

- [x] Trocar de personagem muda o que o botão esquerdo faz
- [x] O tiro acerta o ponto mirado, com dispersão pequena e rastro visível
- [x] A munição diminui e a recarga acontece ao esvaziar
- [x] Segurar o botão dispara em cadência constante
- [x] Aumentar o atributo de cadência acelera o disparo **sem** alterar os dados
      da arma
- [x] A troca de personagem não introduz nenhuma ramificação sobre o nome do
      personagem no código

## Comments

Este é o ticket que **prova a arquitetura data-driven**. Se ele exigir um
`if (personagem == …)` em qualquer lugar, a abstração de arma do ticket 08 está
errada e é aqui que se descobre — não no M7, com sete sistemas em cima.

O critério da cadência existe por antecipação: a transformação Overdrive, no M4,
aumenta a cadência da pistoleira em 50 %. Se isso exigir editar os dados da
arma, o bloco de atributos do ticket 07 não está sendo lido no lugar certo.

Sem queda de projétil: tiro instantâneo. O único projétil real do jogo é uma
habilidade da pistoleira, no M3.

---

**Concluído em 2026-09-05.** [`HitscanWeapon`](../../../src/Weapons/HitscanWeapon.cs)
implementa `IWeapon` (ticket 08) e nasce pela mesma
[`WeaponFactory`](../../../src/Weapons/WeaponFactory.cs) que já produzia
`MeleeWeapon` — o `CombatComponent` continua sem saber o que está segurando.
Isso **é** a prova de arquitetura que o ticket pedia: a mesma cena
(`Character.tscn`), o mesmo `CombatComponent`, o mesmo M1, produzindo golpe de
espada ou tiro de revólver só a partir de qual `.tres` está em
`CharacterDefinition.Weapon`.

**O que foi construído:**

- [`RevolverState`](../../../src/Weapons/RevolverState.cs) — POCO com tambor,
  cadência e recarga automática, testado em xUnit (12 casos) pelo mesmo motivo
  do `MeleeCombo`: janela de tempo é onde erro de comparação passa
  despercebido. O intervalo entre tiros é passado a cada chamada, nunca
  fixado — é o que faz `AttackSpeed` do `StatBlock` ter efeito sem tocar no
  `.tres`.
- [`HitscanMath.TryHitSegment`](../../../src/Weapons/HitscanMath.cs) — geometria
  pura (ponto mais próximo de um segmento, limitado a `[0, alcance]`), 7 testes
  xUnit. O acerto em personagem é geometria contra posição, igual ao corpo a
  corpo do ticket 08 — o projeto ainda não tem hurtbox física em lugar nenhum.
  Só a distância até PAREDE usa um raycast real (`PhysicsLayers.World`).
- `WeaponDefinition` ganhou `AttackInterval`, `MagazineSize`, `ReloadTime` e
  `SpreadDegrees` — campos só-hitscan, ao lado dos só-melee que já existiam
  (`HalfAngle`, `ComboSteps`).
- `IWeapon.Tick` ganhou o parâmetro `triggerHeld`: corpo a corpo ignora (cada
  passo do combo exige um clique novo), hitscan atira em cadência enquanto
  for true. O sinal vem de `IntentFrame.AttackHeld`
  (`Input.IsActionPressed`), novo ao lado do `AttackPressed` que já existia.
- `HitscanWeapon.IsAttacking` é **sempre falso**, de propósito: atirar não
  trava `Movement`/`Rotation` como o combo trava — nada pedia isso, e um
  revólver que imobiliza a cada tiro jogaria mal. A `ActionLockSet` do
  ticket 08 simplesmente nunca é acionada para esta arma; nenhum código novo
  foi necessário para essa diferença de comportamento.
- Tambor cheio → 6 tiros conectam → o 7º (ainda dentro dos 1,6 s de recarga)
  falha → depois de recarregar, conecta de novo. Segurar o gatilho mantém a
  cadência via o `AttackHeld` automático do `CharacterController`, sem
  precisar de um clique por tiro.
- Rastro visual: um `MeshInstance3D`/`BoxMesh` por tiro, live por 0,06 s,
  placeholder deliberado até o VFX pooled do ticket 36.
- Dados: [`revolver.tres`](../../../data/weapons/revolver.tres) (18 dano,
  alcance 25 m, cilindro 6, recarga 1,6 s, intervalo 0,30 s — valores da
  spec 08 §3) e
  [`gunslinger.tres`](../../../data/characters/gunslinger.tres) (MoveSpeed
  6,2 m/s, 100 HP, também da spec 08 §3). O `Defense` de 0,85 da gunslinger
  **não** foi aplicado — vive num `[Export]` da `StatsComponent` na cena, não
  no `.tres`, e ajustar isso é escopo de personalização por instância de cena
  (ticket 31, seleção de personagem), não deste ticket.

**Verificado por [`RevolverProbe`](../../../src/Tools/RevolverProbe.cs)**, na
árvore real, headless, contra `RevolverArena.tscn` (cena dedicada, menor que a
`Arena` principal): tambor de 6 esvaziando, bloqueio durante a recarga,
recarga completando sozinha, cadência mantida ao segurar o botão (via
`Input.ActionPress` real, não uma chamada direta — exercita
`PlayerInputController` → `IntentFrame` → `CombatComponent` de ponta a ponta),
e a cadência **dobrando** ao dobrar `AttackSpeed` via `StatModifier`, sem
tocar no `revolver.tres`. O manequim desta cena usa uma
`HealthDefinition` própria (5000 HP) — o padrão de 100 HP morre antes do
sétimo tiro da sonda, e um alvo morto some da varredura de alvos do
`HitscanWeapon`, o que mascararia os testes de recarga/cadência como falha de
acerto.

**Não verificado nesta sessão:** o rastro visual e a sensação de disparo em
playtest manual no editor — a CLI não renderiza. A física de bloqueio por
parede (`AlcanceAteParede`) está implementada e é direta (raycast contra
`PhysicsLayers.World`), mas não tem uma verificação automatizada dedicada;
ficou de fora para não inflar o escopo da sonda além do que o ticket pede.
