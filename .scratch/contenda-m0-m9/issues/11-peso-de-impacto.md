# 11: Peso de impacto — hitstop, knockback, flash e números

**What to build:** bater deixa de ser um número mudo e passa a ser satisfatório.
O golpe congela por um instante, empurra o alvo, faz o material piscar e mostra o
dano subindo na tela.

**Blocked by:** 08.

**Status:** CONCLUÍDO

- [x] **hitstop por escala de tempo local** dos dois envolvidos — 0.04 s no golpe
      normal, 0.09 s no finalizador. **Nunca** via `Engine.TimeScale`, que
      congelaria a horda inteira junto no M5
- [x] knockback com impulso na direção do golpe, decaindo em 0.25 s
- [x] flash emissivo branco de 0.08 s ao levar dano
- [x] números de dano flutuantes (`Label3D` billboard, **pooled**), subindo 1 m e
      sumindo em 0.6 s
- [x] screen shake aditivo — 0.15 ao acertar, 0.3 ao levar dano — que nunca
      altera a rotação base da câmera
- [x] intensidade do shake respeitando uma configuração (mesmo que o menu só
      chegue no M7)

## Comments

Isto **não** é polimento adiável, e por isso está no M2 e não no M9: é o critério
de pronto do milestone. Se bater num manequim parado não for divertido aqui, não
vai ficar melhor com 40 inimigos em volta.

O hitstop local em vez de global é a decisão que evita retrabalho no M5.

---

**Concluído em 2026-09-05.** Fecha o M2 inteiro (bloco "combate básico" —
tickets 08 a 12 concluídos).

**O que foi construído:**

- [`HitstopState`](../../../src/Components/Health/HitstopState.cs) vive em
  `HealthComponent`, não em `CombatComponent`: hitstop tem que congelar os
  DOIS envolvidos, e um manequim de treino tem vida mas não tem combate. A
  arma chama `ApplyHitstop` dos dois lados do golpe. A DURAÇÃO é dado, não
  código — um campo `HitstopSeconds` em `MeleeComboStep` (0,04 nos slashes 1-2,
  0,09 no slash 3) e em `WeaponDefinition` (hitscan, sem cadeia). O finalizador
  não precisou de nenhum `if`: é só um passo com um número maior.
- **Bug real encontrado e corrigido durante a integração:** escalar o delta do
  PRÓPRIO `CombatComponent.Tick` pelo hitstop criava um loop — um tiro/golpe
  aciona hitstop, que zera o delta do combate no quadro seguinte, que atrasa o
  cooldown/combo de quem acabou de acertar, atrasando o próximo tiro/golpe a
  cada vez mais. Só `MovementComponent` é escalado; combate roda sempre em
  delta cru. As sondas `RevolverProbe`/`CombatProbe` pegaram isso na hora —
  ambas começaram a falhar com a cadência do revólver e o encadeamento do
  combo bagunçados assim que o hitstop entrou.
- [`KnockbackState`](../../../src/Components/Movement/KnockbackState.cs)
  substitui a repulsão por taxa constante (m/s²) do ticket 07/08 por decaimento
  em DURAÇÃO fixa (0,25 s) — a spec pede que um golpe fraco e um forte parem
  de empurrar no MESMO tempo, e a implementação antiga não fazia isso. A
  velocidade de decaimento é fixada uma vez a cada `Apply`, como o
  `LungeMotion` já faz para o avanço — recalculá-la a cada quadro a partir do
  que resta produziria a mesma curva exponencial que o `DamageLayerState`
  evitou no ticket 12 (achado ANTES de virar bug, desta vez, por já saber
  procurar por ele).
- [`DamageFlashComponent`](../../../src/Components/Health/DamageFlashComponent.cs)
  usa `MeshInstance3D.MaterialOverride`, não o material do `Mesh` direto: os
  três manequins da arena compartilham a MESMA malha, e escrever no material
  base piscaria todo mundo de uma vez.
- [`FloatingDamageMath`](../../../src/Vfx/FloatingDamageMath.cs) +
  [`DamageNumberPool`](../../../src/Vfx/DamageNumberPool.cs) — pool de
  `Label3D` fixo, criado uma vez no boot, reciclado em round-robin preferindo
  slot ocioso. Autoload que ouve `GameEvents.DamageNumberRequested` sozinho;
  nenhuma arma ou personagem sabe que ele existe. É o primeiro evento real do
  barramento — até aqui `GameEvents` só existia como classe vazia,
  deliberadamente (YAGNI), esperando o primeiro consumidor de verdade.
- Screen shake é comunicação DIRETA, não pelo barramento: `CameraRig` já
  segura uma referência estrutural ao alvo (`Target`), então assina
  `Combat.HitLanded`/`Health.Damaged` dele direto no `_Ready` (adiado com
  `CallDeferred`, porque rig e personagem são irmãos e a ordem entre eles não
  é garantida). `ShakeIntensityMultiplier` entrou em `CameraSettings` e é
  aplicado DENTRO de `Shake()`, não em cada chamador — o único knob que o
  menu de acessibilidade do M7 vai precisar expor.

**Verificado por [`ImpactProbe`](../../../src/Tools/ImpactProbe.cs)**, headless,
na árvore real, a partir de UM golpe de combate de verdade (não uma chamada
direta a `ApplyDamage`): os dois lados congelam, o alvo é empurrado e para de
deslizar no tempo certo, a malha pisca e apaga, um número de dano aparece e
some, e a câmera treme — as cinco coisas disparando juntas, como vão disparar
no jogo de verdade. As verificações de tempo são relativas ao QUADRO EM QUE O
GOLPE CONECTOU, não a um número fixo — hitstop dura só 2-5 quadros, e checar
num quadro escolhido sem saber quando o golpe realmente conectou teria
reportado falsos negativos.

**Não verificado nesta sessão:** a SENSAÇÃO de tudo isso — se o hitstop parece
bom, se o shake incomoda ou não, se o flash é visível o bastante. A CLI não
renderiza. Os números (0,04/0,09/0,25/0,08 s, 0,15/0,3 de shake) são os da
spec; ajustá-los por playtest é o próximo passo natural, e tudo está em
`.tres`/`[Export]`, sem precisar recompilar.
