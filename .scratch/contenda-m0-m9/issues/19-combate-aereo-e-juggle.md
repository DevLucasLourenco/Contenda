# 19: Golpes no ar mantêm o inimigo lançado no alto

**What to build:** o circuito que fecha o combo vertical do espadachim. Lançar o
inimigo com o anti-aéreo, persegui-lo pulando, mantê-lo no ar com golpes rápidos
e terminar com um mergulho que explode em área ao aterrissar.

**Blocked by:** 17, 15

**Status:** done

- [x] Atacar no ar dá uma cadeia curta, mais rápida e mais fraca que a de solo
- [x] Cada acerto aéreo dá um empurrãozinho para cima no alvo **e no atacante**,
      sustentando o combo
- [x] A gravidade cai durante a janela do golpe — o ataque segura no ar, sem virar voo
- [x] Segurar o ataque depois do pico faz o personagem mergulhar
- [x] O mergulho causa dano em área ao aterrissar, com repulsão
- [x] O mergulho tem recuperação longa ao tocar o chão — é o risco que paga o poder
- [x] Um inimigo lançado **para de tentar andar** enquanto está no ar (sinal pronto
      -- ver Implementação sobre a IA em si ainda não existir)
- [x] Nenhum inimigo passa de quatro acertos aéreos seguidos: depois disso ele cai
- [x] O anti-aéreo existente encadeia naturalmente com tudo isso

## Comments

O teto de quatro acertos existe para que o modo horda não vire exibição de
malabarismo: sem ele, um jogador habilidoso prende um inimigo no ar
indefinidamente enquanto os outros trinta esperam.

O estado "no ar" do inimigo é novo na máquina de estados e precisa entrar de
qualquer estado — inclusive de perseguição e de ataque.

## Implementação

`MeleeWeapon` (única arma que ganha combate aéreo — é o espadachim quem tem
esse combo vertical, não a pistoleira) passa a manter DUAS `MeleeCombo`
paralelas: `_combo` (solo, como já existia) e `_comboAereo` (nova, 2 passos).
Qual delas está ativa é decidido uma vez só, ao começar uma cadeia do zero —
se o personagem já está no ar naquele instante e a arma tem
`AerialComboSteps` definido — e a cadeia continua no mesmo modo até acabar,
mesmo que o personagem aterrisse ou pule no meio dela.

`WeaponDefinition` ganhou um bloco de campos só para isso: `AerialComboSteps`
(dois passos novos, mais fracos e mais rápidos que os de solo — 0.7×/0.77×
de dano contra 1.0×/1.1×, janelas de acerto de ~0.10 s contra ~0.14 s),
`AerialRange`/`AerialHalfAngle`/`AerialVerticalReach` (cone menor, mas folga
VERTICAL maior — um alvo em pleno juggle sobe e desce mais do que um golpe
de solo prevê) e `AerialVerticalKnockback`/`AerialGravityScale` (os dois
números exatos da spec 16 §6: 0.8 m/s e 0.35×).

**Gravidade reduzida**: `MovementComponent` ganhou `ExternalGravityScale`
(1 é neutro), multiplicando `Settings.Gravity` antes de `ApplyGravity`.
`MeleeWeapon` escreve um valor definido nele em TODO `Tick` — a redução
enquanto a janela aérea estiver aberta, 1 fora disso — nunca só quando o
efeito está ativo, para nunca ficar preso num valor errado se a cadeia for
cancelada no meio.

**Impulso vertical e teto de juggle**: `HealthComponent` ganhou
`RegistrarAcertoAereo()`, delegando para o novo POCO `AerialJuggleState`
(testado em xUnit): incrementa um contador e devolve se ainda está sob o
teto de 4. `MeleeWeapon.ResolverAcertos()` só aplica `ApplyKnockback(Vector3.Up * 0.8)`
no alvo E em si mesmo quando esse método devolve `true` — depois do 4º
acerto, os dois param de subir e a gravidade normal (sem redução —
a janela de um golpe que nem vai empurrar mais continua reduzindo a
gravidade, mas isso é cosmético, não estrutural) retoma sozinha. O contador
zera quando o alvo TOCA O CHÃO (`MovementComponent` chama
`Health.ResetAerialJuggle()` a cada quadro que estiver apoiado) — mais
simples e igualmente correto que tentar detectar a borda exata de
"acabou de aterrissar", porque ninguém é lançado de novo sem antes deixar
de estar no chão.

**Estocada de queda**: `AerialCombatMath.DeveComecarMergulho` (POCO puro,
testado em xUnit) decide, a partir de "no ar + já passou do pico
(velocidadeY ≤ 0) + segurando o ataque + sem golpe em andamento + ainda não
mergulhando". `IntentFrame.AttackHeld` já chegava a `MeleeWeapon.Tick` desde
o ticket 09 (a arma hitscan já o usava para cadência automática) — o corpo
a corpo só passou a consultá-lo. Ao mergulhar, `MeleeWeapon` crava
`ExternalGravityScale` num multiplicador BEM maior que 1 (queda acelerada,
o oposto exato do golpe aéreo) e desliza para a frente a cada quadro
reaproveitando o mesmo `Avancar()` do wind-up do golpe comum — é a
combinação dessas duas coisas que produz a diagonal da spec, sem nenhuma
trajetória calculada à parte. `MeleeWeapon.IsAttacking` passa a valer
`true` também durante o mergulho: é só isso que faz `CombatComponent`
travar Movimento e Rotação sozinho por cima dele, com o mesmo refresco por
quadro que já usa para qualquer golpe — nenhuma trava nova precisou ser
ensinada a ele. Ao pousar (checado por `MovementComponent.IsGrounded`, ver
abaixo), a área de dano (raio 3.5 m, dano autoral de 105 no `.tres` — não
derivado em código da relação "1.4× de Heavy Lunge" que a spec descreve,
regra 4 do CLAUDE.md) sorteia um crítico compartilhado para a explosão
inteira (disciplina do ticket 18) mas o SOBRESCREVE para sempre-crítico
contra qualquer alvo que ainda esteja no ar — um fato objetivo, não um
segundo sorteio, então não reintroduz a "loteria" que aquele ticket evita.
A recuperação (0.4 s, trava Movimento+Rotação) é pedida direto por
`CombatComponent.ApplyLock`, mesmo mecanismo que o dash e as habilidades já
usam.

**Achado real de bug, descoberto ao escrever o probe**: `RequestBasicAttack()`
decidia "estou no ar?" chamando `corpo.IsOnFloor()` direto. Um
`CharacterBody3D` que ainda não rodou nenhum `MoveAndSlide` devolve falso
ali — e um probe que pede um ataque no MESMO quadro em que o personagem
entra na árvore (todo probe deste projeto faz exatamente isso) via essa
leitura "no ar" por engano, roteando um golpe de SOLO para o combo aéreo
sem nenhum aviso. Corrigido dando a `MovementComponent` um `IsGrounded`
cacheado, atualizado uma vez por `Tick` a partir do mesmo `IsOnFloor()`, mas
com o padrão em `true` (mais seguro: um personagem parado no primeiro
instante da própria existência está muito mais perto de "no chão" do que
de "no ar") em vez do `false` que a engine devolveria sozinha. Isto também
tornou `CombatProbe`/`ImpactProbe` (tickets 08/11) mais robustos de graça,
mesmo eles nunca tendo tocado em combate aéreo.

**Sobre o item "inimigo para de tentar andar no ar"**: não existe NENHUMA
IA neste projeto ainda (chega no M5, ticket 22, que não é sequer bloqueador
deste ticket). Não há o que "parar de tentar andar" hoje — o único
"inimigo" é o manequim de treino, que nunca andava para começo de conversa.
O sinal que uma IA futura vai precisar para isso já existe e está correto:
`MovementComponent.IsGrounded` (falso enquanto lançado) é exatamente a
condição que o ticket 22 vai consultar para suprimir navegação. Mesmo
padrão de "constrói o sinal agora, a reação vem depois" que o ticket 17 já
usou para o dash escapar de um atordoamento que também ainda não existe.

Verificado por `AerialCombatProbe` (headless, árvore real, `Arena.tscn`):
o `.tres` da espada carrega um combo aéreo mais curto/fraco/rápido que o de
solo, um golpe de solo real causa mais dano que um aéreo real, a gravidade
do atacante lê o valor reduzido durante a janela, o impulso vertical no
alvo E em quem golpeia bate com o valor da spec (medido por COMPARAÇÃO
entre duas rodadas idênticas — uma com o teto de juggle livre, outra
pré-esgotado via `RegistrarAcertoAereo` direto — porque a repulsão do
`KnockbackState` não é um empurrão instantâneo, e comparar as duas rodadas
absorve isso sem precisar reproduzir a integração da gravidade à mão), o
teto de juggle realmente desliga os dois impulsos depois do 4º acerto, e
segurar o ataque depois do pico mergulha, causa dano em área ao pousar e
trava a recuperação.

### Code review

Um achado de spec real e corrigido: `_emCombateAereo` fica fixo pelo resto
da cadeia depois de decidido (de propósito — ver acima), mas o impulso
vertical em `ResolverAcertos` só checava `_emCombateAereo`, não se quem
golpeou estava REALMENTE no ar naquele instante. Um passo de uma cadeia
aérea que conecta depois de quem golpeou já ter aterrissado (janelas de
acerto de ~0,1-0,2 s tornam isso plausível) lançaria um personagem já no
chão (e o alvo) para cima do nada — a spec fala em SUSTENTAR um combo que
já está no ar, nunca em decolar alguém do chão. Corrigido acrescentando
`!EstaApoiado()` à condição; o resto da cadeia (dano/alcance mais fracos)
continua aéreo até o fim, só o impulso vertical passou a exigir estar no ar
de verdade no instante do acerto.

Um achado de standards aplicado: a property `ComboAtivo` (nova) tinha ficado
entre os `event` e a API pública, fora da ordem de convenções §7
(constantes → `[Export]` → campos → properties públicas → eventos → ciclo
de vida → API pública → auxiliares privados). Movida para perto de
`PassoDe`, na seção de auxiliares privados, onde já pertencia.

Dois achados de standards levantados e conscientemente adiados, não
corrigidos: (1) `MeleeWeapon` cresceu para three responsabilidades (combo de
solo, combo aéreo, mergulho) e o mergulho compartilha pouco com a máquina de
combo — um candidato real a virar um colaborador à parte, no mesmo espírito
de `LungeMotion`/`MeleeCombo`; (2) os ~15 campos novos de `WeaponDefinition`
(`Aerial*`/`Dive*`) formam dois "clumps" de dados que poderiam virar
sub-recursos próprios. Os dois são reorganizações de forma, não bugs, e o
sistema atual já passou por uma depuração extensa e sensível a detalhes de
ordem de quadro (ver os achados de `IsGrounded`/`KnockbackState` abaixo) —
mexer na estrutura agora sem necessidade arrisca reintroduzir exatamente o
tipo de regressão sutil que este ticket levou mais tempo caçando do que
escrevendo. Fica registrado para uma próxima passada de limpeza, não para
este commit.

**Achado real de bug, descoberto ao escrever o probe** (já descrito acima
em Implementação, repetido aqui por completude do código-review): o
`IsGrounded` cacheado em `MovementComponent` corrigiu não só
`MeleeWeapon.RequestBasicAttack`, mas também expôs uma falha LATENTE em
`CriticalProbe` (ticket 18): seu primeiro pedido de golpe, no quadro 1 da
própria fase, também corria antes do jogador assentar no chão — inofensivo
enquanto `AerialComboSteps` estava vazio (nunca haveria para onde rotear),
mas passou a rotear o primeiro golpe forçado para o combo aéreo assim que
este ticket deu dados reais a `sword.tres`, fazendo `CriticalProbe` reportar
2.0x esperado contra 1.4x observado (20 → 28, a matemática do combo aéreo
mais fraco por trás). Corrigido dando a `CriticalProbe` a mesma folga de
alguns quadros antes do primeiro pedido que `CombatProbe`/`ImpactProbe` já
tinham ganhado.

Regressão completa (278 testes xUnit, os 11 probes headless -- incluindo
`CriticalProbe`, corrigido -- `ExportRelease` e o checador anti-2D)
reexecutada depois dos ajustes de review; tudo verde.
