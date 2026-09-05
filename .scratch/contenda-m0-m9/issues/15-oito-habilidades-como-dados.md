# 15: Os dois personagens têm quatro habilidades cada, todas como dados

**What to build:** o repertório completo do MVP. Investida, anti-aéreo, área e
finalizador, para espadachim e pistoleira — com efeitos genuinamente diferentes:
corte em cone, avanço com dano no trajeto, lançamento para cima, tiro
instantâneo, rajada e projétil explosivo.

**Blocked by:** 14, 09

**Status:** done

- [x] As oito habilidades existem e funcionam contra o manequim
- [x] As quatro sequências significam a mesma coisa nos dois personagens, para
      que a memória muscular seja transferível
- [x] Nenhuma habilidade é referenciada por nome dentro do código
- [x] Um novo tipo de efeito entra sem alterar quem executa habilidades
- [x] Uma nona habilidade hipotética exige **apenas** um arquivo de dados e uma
      entrada na lista do personagem
- [x] Nenhuma sequência de um personagem é prefixo de outra dele que já dispare
- [x] O projétil explosivo é reaproveitado de um pool, não criado e destruído

## Comments

O critério que separa sucesso de fracasso aqui não é "as oito funcionam" — é a
ausência de código específico por habilidade. Se `DashSlash` aparecer como nome
de classe ou de `case`, o desenho data-driven falhou mesmo com o jogo rodando.

## Implementação

Seis `IAbilityBehavior` novos, um por `Kind` restante (`MeleeArcBehavior`,
`UppercutBehavior`, `HitscanShotBehavior`, `HitscanBurstBehavior`,
`ProjectileBehavior`; `DashAttackBehavior` já existia do ticket 14) —
`AbilityBehaviorRegistry` despacha por `Kind`, nunca por `Id`/nome. Quick Step
Shot **não** ganhou um `Kind` próprio: é o mesmo `DashAttackBehavior`, só com
`Range` (14 m) maior que `DashDistance` (4 m) — a spec 05 §5 descreve como
"DashAttack + Hitscan", mas o enum §3 não tem um `Kind` combinado, e o
comportamento já varria até `Max(DashDistance, Range)`.

`HitscanBurstBehavior` reaproveita `MaxTargets` para "número de tiros" em vez
de "quantos alvos" — mesma ambiguidade deliberada que `Range`/`Radius`/`Angle`
já têm entre `Kind`s diferentes, documentada na classe.

`ProjectilePool` (autoload novo, `src/Components/Abilities/ProjectilePool.cs`)
segue o desenho do `DamageNumberPool` do ticket 11: slots fixos criados uma vez
no boot, reciclados em round-robin, ouvindo `GameEvents.ProjectileFireRequested`
sozinho — nenhum `ProjectileBehavior` guarda referência ao pool. Sem colisão
contra paredes ainda (deferido; a arena de hoje é aberta o bastante).

Não é `Area3D` como a spec 07 §9 sugere: o resto do combate do projeto inteiro
resolve acerto por geometria contra grupo (`MeleeWeapon`, `HitscanWeapon`,
`DashAttackBehavior`), nunca por hurtbox física — introduzir só aqui um
segundo mecanismo custaria mais consistência do que ganharia.

Verificado por `AbilityCatalogProbe`/`AbilityCatalogArena` (headless, árvore
real): as oito habilidades — quatro do Swordsman, quatro da Gunslinger —
executam pela própria sequência (lida do `.tres`, não hardcoded no probe) e
causam dano ao manequim; o pool de projéteis não cria nem destrói nó ao
disparar além do próprio tamanho.

### Code review

`AbilityGeometry` (testável em xUnit) e `AbilityTargeting` (fronteira com a
engine, como o `MeleeWeapon.AmostrarAlvos`) saíram do review: "frente
achatada"/"mira com fallback" e "varrer grupo por alvo válido" se repetiam
quase idênticas em seis classes novas no mesmo ticket. Todos os
`IAbilityBehavior` novos e o `ProjectilePool` foram refeitos em cima delas.

Achado mais sério: `ProjectilePool` chamava `GetNodesInGroup` a cada quadro
por projétil em voo — exatamente a alocação por quadro que as convenções §5 e
o `MeleeWeapon` já proíbem. Corrigido amostrando o grupo **uma vez, no
disparo** (`_alvosEmCache`, uma `List&lt;CharacterController&gt;` reaproveitada
por slot), a mesma escolha de "quem estava lá quando começou" que o combo
corpo a corpo já fazia — não mais alocação nenhuma por quadro.
