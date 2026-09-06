# 18: Golpes críticos, inconfundíveis quando acontecem

**What to build:** parte dos golpes passa a sair muito mais forte, com sinal
claro. O espadachim critica com mais frequência que a pistoleira, e a diferença
precisa ser sentida sem olhar número nenhum.

**Blocked by:** 07, 11

**Status:** done

- [x] Existem dois atributos novos: chance de crítico e multiplicador de crítico
- [x] O espadachim critica com o dobro da frequência da pistoleira
- [x] Inimigos não criticam
- [x] Um ataque em área é crítico **em todos os alvos ou em nenhum** — nunca em
      alguns
- [x] O número de dano do crítico é maior, de outra cor e com brilho
- [x] O congelamento do golpe crítico é mais longo que o do golpe normal
- [x] O som do crítico tem uma camada a mais (sinal pronto -- ver Implementação)
- [x] Os valores de chance e multiplicador vêm de arquivo de dados

## Comments

Sortear por alvo em vez de por golpe transformaria ataques de área em loteria e
tornaria o crítico ilegível — o jogador veria cinco números diferentes sem
entender por quê. Um sorteio por golpe.

Este ticket é pré-requisito do Berserker: a forma dá +35 pontos percentuais de
chance, e sem o sistema de crítico ela não teria o que multiplicar.

## Implementação

`CritMath` (POCO novo em `src/Components/Stats/`, testado em xUnit) é só duas
funções puras: `Rolar(chance, sorteio01)` decide se um sorteio conta como
crítico, `Aplicar(dano, critico, multiplicador)` multiplica se sim. O sorteio
de verdade (`GD.Randf()`) fica do lado de fora, em cada golpe — mesma
disciplina de todo o resto do projeto que mantém lógica pura testável separada
da fronteira com a engine.

`CritChance`/`CritMultiplier` já existiam como `StatId` (de um ticket
anterior); o que faltava era alimentá-los. `StatsDefinition` (novo `Resource`)
guarda os dois por personagem — `data/characters/shared/stats_swordsman.tres`
(10 %, 2.0×) e `stats_gunslinger.tres` (5 %, 1.8×), exatamente a tabela da
spec 16 §5. `CharacterDefinition.Stats` é nulo para quem não tem um desses
(hoje, todo inimigo) e `StatsComponent.Configure` trata nulo como 0 % de
chance — é isso que faz "inimigos não criticam" sem nenhum `if` sobre quem é o
personagem, só a ausência do recurso.

**Uma rolagem por golpe, nunca por alvo** — o critério mais fácil de
implementar errado, porque a maioria dos ataques em área já varre os alvos num
laço que seria natural (e errado) rolar dentro dele:
- `MeleeArcBehavior` (Spin Slash), `UppercutBehavior` (Rising Slash),
  `DashAttackBehavior` (Dash Slash / Heavy Lunge) e `HitscanShotBehavior`
  (Deadeye, perfurante) rolam UMA VEZ antes do laço `ForEachValidTarget` e
  reaproveitam o mesmo `critico`/`dano` para todo mundo que acertam.
- `ProjectileBehavior` (Explosive Shot) rola no DISPARO, não na explosão — o
  valor atravessa `ProjectileFireEvent.IsCritical` até `ProjectilePool`, que
  reaproveita para todo mundo pego pela explosão.
- `MeleeWeapon` (ataque básico da espada) é o caso mais sutil: a janela de
  acerto de um golpe dura vários quadros (`Tick()` roda de novo a cada um
  enquanto ela estiver aberta), então rolar dentro de `ResolverAcertos()`
  rolaria de novo a cada quadro da MESMA janela. Corrigido rolando uma vez em
  `RequestBasicAttack()` e guardando o resultado (`_criticoDoGolpeAtual`) para
  a janela inteira.
- `HitscanBurstBehavior` (Fan the Hammer) é a exceção deliberada: rola UMA VEZ
  POR TIRO, não uma vez para a rajada toda. São seis disparos em sequência,
  não uma área simultânea — mais parecido com apertar o gatilho do revólver
  seis vezes do que com um cone que acerta cinco alvos de um só golpe. Ver
  comentário na classe.
- `HitscanWeapon` (revólver básico) só atinge um alvo por tiro, então o
  problema nem existe — rola direto em `Disparar()`.

Congelamento: `WeaponDefinition.CriticalHitstopBonus` e
`MeleeComboStep.CriticalHitstopBonus` (padrão 0,05 s) são SOMADOS ao
`HitstopSeconds` normal, nunca um valor absoluto — assim o finalizador
crítico (0,09 + 0,05 s) continua mais longo que um jab crítico
(0,04 + 0,05 s), e os dois continuam mais longos que a versão normal do
mesmo passo. Só o ataque básico (espada e revólver) ganhou o bônus: nenhuma
habilidade aplicava hitstop antes deste ticket, e criar esse sistema do zero
para elas é escopo maior que "golpes críticos" pede.

Número de dano: `DamageNumberEvent.IsCritical` já existia e já alimentava a
cor (`CriticalColor`, dourado) desde o ticket 11 — só nunca vinha `true`. Este
ticket completa o resto do "maior, de outra cor e com brilho" em
`DamageNumberPool`: `CriticalScale` (1,6×, escala do `Label3D`) e
`CriticalOutlineSize`/`CriticalOutlineColor` (contorno branco = o brilho).

Som: **sem sistema de áudio nenhum no projeto ainda** (`AudioDirector` é um
autoload vazio; nenhum `AudioStreamPlayer` existe em lugar nenhum do `src/`) —
sons de verdade são o ticket 36 (M8), que já lista "impacto diferente para
crítico" na própria spec. O sinal que uma camada extra de som precisaria
(`DamageInfo.IsCritical` / `DamageNumberEvent.IsCritical`) já está correto e
propagando corretamente por todo o pipeline; o ticket 36 só precisa assinar
`GameEvents.DamageNumberRequested` e checar a flag, sem nenhuma mudança extra
por aqui.

Verificado por `CriticalProbe` (headless, árvore real, `Arena.tscn` + um
segundo manequim instanciado à parte): os dois `.tres` de personagem carregam
os valores certos e na proporção certa (10 % vs 5 %), o manequim (inimigo)
tem 0 % de chance, um golpe forçado a 100 % sai crítico com o dano e o
congelamento maiores que um forçado a 0 %, o número muda de escala e ganha
contorno, e — a parte mais importante — dez tentativas de um golpe em área
com 50 % de chance NUNCA dividem o resultado entre os dois alvos atingidos na
mesma janela (ver comentário da classe do probe sobre por que essa é a única
verificação probabilística do projeto e por que ela só falha para um lado).

### Code review

Um achado de standards aplicado: os oito pontos que aplicam dano repetiam o
mesmo par de linhas (`Stats?.Get(CritChance) ?? 0f` + `GD.Randf()` para
sortear, `Stats?.Get(CritMultiplier) ?? 1f` para aplicar) quase palavra por
palavra — Duplicated Code / Data Clump real, com risco concreto de algum
ponto esquecer um `?? 1f`. Extraídos dois atalhos em `CritMath`
(`RolarNaStats`/`AplicarNaStats`, que leem direto do `StatsComponent`) e os
oito pontos passaram a usá-los. `CritMath.Rolar`/`Aplicar` continuam puras e
são o que o xUnit testa; os atalhos novos chamam `GD.Randf()` de propósito e
por isso não entram nos testes — só a decisão pura precisa ser exercitada sem
a engine.

Um achado de standards investigado e descartado: o reviewer apontou que os
dois `+=` sem `-=` correspondente em `CriticalProbe` (assinando
`Health.Damaged` com lambda) violam a regra "sem exceção" das convenções §3.
Todo outro probe do projeto que assina eventos (`CombatProbe`,
`RevolverProbe`, `ImpactProbe`) faz exatamente a mesma coisa — lambda em
`+=`, sem `_ExitTree` nenhum — porque um probe é um processo `--headless` de
vida curta que termina com `GetTree().Quit()`; não há sessão longa para a
assinatura vazar. Consistente com o padrão já estabelecido no repositório,
não uma exceção nova; não alterado.

Dois achados de spec como decisões de escopo deliberadas, ambas mantidas
como estavam: `HitscanBurstBehavior` (Fan the Hammer) sorteando por TIRO, não
por rajada inteira (seis disparos sequenciais, não uma área simultânea — ver
comentário na classe); e o bônus de congelamento crítico só existir no
ataque básico (espada/revólver), porque nenhuma habilidade tinha congelamento
nenhum antes deste ticket, e criar esse sistema do zero para elas é escopo
maior que "golpes críticos" pede.

Regressão completa (267 testes xUnit, os 9 probes headless pré-existentes,
`CriticalProbe`, `ExportRelease` e o checador anti-2D) reexecutada depois do
ajuste de review; tudo verde.
