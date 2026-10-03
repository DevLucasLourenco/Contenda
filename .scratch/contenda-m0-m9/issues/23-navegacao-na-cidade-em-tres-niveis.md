# 23: Inimigos atravessam a cidade sem se amontoar nem travar

**What to build:** vários inimigos convergindo para o jogador ao mesmo tempo,
contornando carros e barricadas, descendo para a praça, subindo na caçamba — e
chegando espalhados, não empilhados uns dentro dos outros.

**Blocked by:** 22, 21

**Status:** implementado; pendente medição de 40 inimigos a 60 FPS em máquina de referência

- [x] Inimigos contornam obstáculos em vez de encostar e ficar raspando na parede
- [x] Descem e sobem as rampas da praça sem travar em quina
- [x] Sobem nos objetos escaláveis pelas ligações de navegação
- [x] Vinte inimigos convergindo num ponto chegam **espalhados**, não sobrepostos
- [x] Um inimigo sem caminho válido não congela: avança na direção do jogador e
      tenta de novo
- [x] O recálculo de rota é distribuído no tempo, não todos no mesmo quadro
- [x] Inimigo distante pensa menos vezes por segundo que inimigo próximo
- [ ] Quarenta inimigos ativos mantêm 60 quadros por segundo (medição gráfica em máquina de referência pendente)

## Comments

Amontoado é o que mais estraga combate corpo a corpo com câmera fixa: o jogador
perde a leitura de quantos são e de onde vem o golpe. Evitação de colisão
sozinha não resolve — precisa de uma força de separação leve por cima.

Recalcular rota de quarenta inimigos no mesmo quadro produz um engasgo visível.
Distribuir a fase entre eles é mais barato que qualquer otimização posterior.

## Implementação

Ticket 22 já dava a um inimigo isolado percepção, perseguição e ataque; este
ticket é sobre o que só aparece com VÁRIOS ao mesmo tempo, numa cidade com
desnível — nada disso existia ainda porque ticket 22 precede a arena urbana
do ticket 21.

### Recálculo de rota escalonado (`NavigationMotor`)

`SetTarget` é chamado a cada quadro (o alvo é a posição do jogador, que muda
quase sempre), mas só repassa para `NavigationAgent3D.TargetPosition` de
verdade quando `NavigationTimingMath.ShouldRepath` manda: o intervalo
(0,25 s) venceu OU o alvo andou `RepathDistance` (1,5 m) desde o último
recálculo — nunca as duas coisas por quadro. O relógio interno nasce numa
fase ALEATÓRIA dentro do intervalo (não em zero), rearmada a cada
`ResetForSpawn` -- é o próprio escalonamento: com o relógio zerado, uma onda
inteira nascendo no mesmo quadro cruzaria o intervalo junto, recalculando
todos de uma vez.

`NavigationTimingMath.ShouldRepath` é uma função pura, testada em xUnit
(`NavigationTimingMathTests`) sem `NavigationAgent3D` nenhum envolvido.

### Sem caminho válido, anda reto em vez de travar

`GetDesiredDirection` checa `NavigationAgent3D.IsTargetReachable()`; se
falso (alvo fora da malha, ou temporariamente isolado), devolve a direção
CRUA até o último alvo pedido em vez de `Vector3.Zero`. O próprio
`SetTarget` já vai tentar de novo assim que o intervalo de repath vencer —
"tenta de novo" não precisa de lógica própria, é o mesmo escalonamento
acima. Verificado chamando `NavigationMotor` direto (não pela percepção
inteira) contra um alvo fora do mapa de 60×60 m, em `EnemyCrowdProbe`.

### Sobem nos objetos escaláveis: faltava o pulo, não a rota

Exigido por spec 17 §5 ("Ligações de navegação... para saltos que os
inimigos podem fazer: rua → caçamba, caçamba → andaime") e §8 ("Inimigos
sobem na caçamba pelas ligações de navegação, sem travar em quina") -- não
é scope creep deste ticket, é o mesmo item do checklist ("sobem nos objetos
escaláveis pelas ligações de navegação") com a origem correta citada.

A ligação de navegação (`NavigationLink3D`, ticket 21) já dava um caminho
GEOMÉTRICO válido de rua até o topo do ônibus/andaime -- o que faltava era o
CORPO saber que precisa de um pulo de verdade para acompanhar. Sem isto, um
inimigo perseguindo andava até a base do objeto escalável e parava ali,
esbarrando na parede vertical, "travado" bem no sintoma que este ticket
existe para eliminar (encontrado rodando o probe deste ticket contra a
escalada real, não só contra o caminho geométrico que `UrbanArenaProbe`, do
ticket 21, já confirmava existir).

Corrigido com `NavigationMotor.PrecisaPular`: se o próximo ponto do caminho
sobe mais que `LimiarDeSalto` (0,5 m -- acima do desnível normal de
rampa/degrau, que o próprio `MoveAndSlide`/colagem ao chão já resolve
sozinho), `EnemyBrain` pede `IntentFrame.JumpPressed`, o MESMO sistema de
pulo do jogador (`JumpState`, spec 09 §1 -- um único caminho de locomoção).
Pedir a cada quadro enquanto ainda precisar não duplica o pulo nem
interrompe um já em andamento: `JumpState.RequestJump` só reabastece um
buffer, consumido só quando `CanJump` (grounded/coyote) permite.

### Força de separação (não empilhar)

`SeparationMath.ComputeForce` (função pura, testada em xUnit) soma um
empurrão para longe de cada vizinho a menos de `RaioDeSeparacao` (1,2 m),
mais forte quanto mais perto, multiplicado por `PesoDeSeparacao` (0,35) --
os mesmos números da spec 09 §4. `EnemyBrain` soma essa força à direção de
navegação ANTES de normalizar para movimento (desvia a direção, não a
velocidade), usando `EnemyPool.ObterPosicoesAtivas` (novo método, escreve
num buffer do próprio `EnemyBrain` reaproveitado a cada quadro -- ver
"Achado de code review" abaixo) para enxergar os outros inimigos ativos
agora, sem cada `EnemyBrain` ter que descobrir vizinhos por conta própria.

**A `AvoidanceEnabled = true` do `NavigationAgent3D` (spec 09 §4) continua
só configurada, não ativada de verdade.** A spec descreve a separação como
"além da avoidance do agente" -- um reforço sobre uma RVO já funcionando --
mas RVO em Godot só entra em ação se o código chamar
`NavigationAgent3D.SetVelocity()` a cada quadro e consumir o
`velocity_computed` que ela devolve; nada neste ticket (nem em nenhum
anterior) faz isso. `avoidance_enabled = true` em `EnemyGrunt.tscn`, sozinho,
não move ninguém. **É a força de separação, sozinha, que faz toda a
espalhada** -- funciona (`EnemyCrowdProbe` prova por comparação, abaixo),
mas é uma camada, não duas como a spec supõe. Fica registrado como dívida
conhecida, não implementada nesta passada: instrumentar RVO de verdade é uma
mudança de fluxo maior (`SetVelocity`/sinal assíncrono, uma arquitetura
diferente do polling síncrono que `NavigationMotor` usa hoje) para um ganho
que a separação sozinha já cobre no critério de aceite deste ticket.

**Só se aplica em `Chase`**, mesmo padrão de ticket 22 ("persegue, PARA ao
alertar/atacar/descansar"): em `Attack`/`Recover`, `ActionLock.Movement` já
zera qualquer `Move` computado, então aplicar separação ali não moveria
ninguém mesmo. Isto tem uma consequência real, não escondida: com
`AttackRange` (2,2 m) bem maior que `RaioDeSeparacao` (1,2 m), a
circunferência de alcance de ataque ao redor do jogador só tem espaço para
~11 inimigos respeitando 1,2 m de vizinho a vizinho -- com uma horda de 16+
convergindo, ALGUM grau de aperto ao entrar em ataque é geometricamente
inevitável, não uma falha do mecanismo. `EnemyCrowdProbe` verifica isto por
COMPARAÇÃO (mesma técnica de `AerialCombatProbe.VerificarImpulsoPorComparacao`),
não por um limiar fixo: a MESMA horda, nas MESMAS condições, com
`PesoDeSeparacao = 0` de um lado e o padrão do outro, e confere que a versão
com separação chega mais espalhada (maior distância média até o vizinho mais
próximo) que sem ela -- prova que o mecanismo funciona, sem prometer um
espaçamento absoluto que a própria geometria (contagem × raio de ataque) não
permite garantir.

### LOD de IA (pensa menos vezes por segundo quando distante)

`EnemyBrain.Poll` já calcula a distância ao jogador antes de qualquer coisa
cara (percepção com raycast, máquina de estados, navegação); acima de
`DistanciaDeLod` (35 m), só "pensa" de verdade a cada
`IntervaloDeLodSegundos` (0,2 s = 5 Hz), reaproveitando a última
`IntentFrame` calculada no meio tempo. O relógio nasce "vencido" (não em
zero) em `Configure`/`ResetForSpawn`, então o primeiro `Poll` da vida sempre
pensa de verdade, mesmo já nascendo longe.

**Achado corrigindo isto**: pular quadros de pensamento também pulava
quadros de DELTA para `EnemyStateMachine.Advance`/o preparo do golpe -- um
inimigo que só pensava a cada 12 quadros alimentava o cronômetro interno de
`LoseTargetDelay` com só 1/12 do tempo de verdade passado, fazendo-o nunca
desistir de perseguir dentro do prazo da spec. Corrigido usando
`_relogioDePensamento` (tempo acumulado desde o ÚLTIMO pensamento de
verdade) como o delta de tudo que só roda quando pensa, não o delta cru do
quadro -- pego pelo próprio `EnemyBrainProbe` (ticket 22) regredindo depois
da mudança, corrigido antes de seguir adiante.

`PensamentosCompletos` (contador público, só para o probe) confirma a
diferença de verdade em `EnemyCrowdProbe`: um inimigo a >35 m pensa bem
menos de 1x por quadro; o mesmo inimigo, reaproximado, volta a pensar quase
todo quadro.

### Achado à parte: vazamento de referência que virava crash em escala

Testando `EnemyCrowdProbe` (dezenas de inimigos perseguindo ao mesmo tempo,
cada um chamando `Perception.Visivel` todo quadro), `EnemyBrainProbe`
(ticket 22, não tocado por este ticket) passou a falhar de forma
INTERMITENTE com `ERROR: Leaked unsafe reference to object:
PhysicsRayQueryParameters3D` seguido de um crash no desligamento -- em
runs isoladas, sem a escala deste ticket, nunca acontecia (confirmado
rodando o código anterior a este ticket 6× seguidas, sempre limpo).

Causa raiz: `Perception.LinhaDeVisaoLivre` criava um
`PhysicsRayQueryParameters3D` novo (`.Create(...)`) a cada raycast, nunca
reaproveitado -- um `RefCounted` de vida curtíssima, no volume certo (uma
horda inteira, todo quadro), expõe uma corrida de finalização conhecida do
runtime C#/Godot. Corrigido em `src/Components/AI/Perception.cs`: o mesmo
`PhysicsRayQueryParameters3D` é criado uma vez e reaproveitado (só
`.From`/`.To`/`.CollisionMask` mudam a cada chamada) pela vida inteira do
`Perception`. Confirmado com 6 rodadas limpas de `EnemyBrainProbe` e 4 de
`EnemyCrowdProbe` depois da correção, nenhuma delas antes.

### O que não deu para verificar rigorosamente

"40 inimigos mantêm 60 fps" não é algo que este ambiente headless consegue
medir -- sem profiler de verdade rodando aqui, só a garantia de que o
trabalho por inimigo caro (raycast de percepção, recálculo de rota) agora é
throttled/escalonado em vez de rodar cheio todo quadro para todo mundo, que
é o que a spec pede para evitar o engasgo. "O recálculo de rota é
distribuído no tempo" tem o mecanismo implementado e testado em isolamento
(`NavigationTimingMathTests`), mas não há um probe automatizado medindo a
DISTRIBUIÇÃO de verdade entre muitos inimigos ao vivo (exigiria expor
contadores internos só para o teste, ou um profiler de frame time que este
ambiente não tem) -- o comportamento correto (fase aleatória por instância)
está implementado e coberto pela unidade pura, só não pela integração em
escala.

### Code review

Duas sub-agents em paralelo (Standards e Spec) revisaram o diff staged
contra `docs/plans/convencoes-de-codigo.md`/o baseline de smells e contra
este ticket + `docs/specs/09-inimigos-e-ia.md`. Achados reais, corrigidos:

- **Alocação por quadro no hot path.** `EnemyBrain.CalcularForcaDeSeparacao`
  criava uma `List<Vector3>` nova a cada chamada (rodando todo quadro, para
  todo inimigo em `Chase`) e iterava `EnemyPool.Active` por trás de
  `IReadOnlyCollection<T>`, fazendo o enumerador de `HashSet<T>` (um struct)
  ser boxed -- duas alocações por quadro, por inimigo, contra convenções §5 e
  spec 15 §3 ("zero alocação no hot path"). Corrigido: `EnemyPool.Active`
  virou `EnemyPool.ObterPosicoesAtivas(excluir, destino)`, que escreve num
  `List<Vector3>` do CHAMADOR (iterando `_ativos` pelo tipo concreto, sem
  boxing) em vez de devolver uma coleção nova; `EnemyBrain` guarda esse
  buffer reaproveitado (`_vizinhosBuffer`) em vez de alocar um a cada quadro.
  `SeparationMath.ComputeForce` também mudou de `IEnumerable<Vector3>` para
  `List<Vector3>` concreto (com um `for` por índice, não `foreach`) pelo
  mesmo motivo -- o parâmetro por interface faria o boxing acontecer DENTRO
  dela mesma, não resolvendo nada só trocando o lado de fora.
- **Avoidance (RVO) nunca ativada de verdade, disclosure adicionado.** A
  spec descreve a separação como reforço sobre uma RVO já funcionando
  (`AvoidanceEnabled = true`), mas RVO em Godot só entra em ação chamando
  `SetVelocity()`/consumindo `velocity_computed` a cada quadro -- nada neste
  ticket faz isso, e a versão anterior deste documento não deixava isso
  claro. Adicionado um parágrafo explícito na seção "Força de separação"
  acima: é só a força de separação, sozinha, fazendo a espalhada funcionar,
  registrado como dívida conhecida, não uma mudança de comportamento (RVO de
  verdade seria uma reestruturação maior de `NavigationMotor`, fora do
  escopo desta passada).
- **Citação da origem do pulo corrigida.** O mecanismo de pulo em ligação de
  navegação vem de spec 17 §5/§8, não só de spec 09 §1 (que só explica POR
  QUE reaproveitar `JumpState` em vez de duplicar); a seção correspondente
  agora cita a origem certa, deixando claro que não é scope creep.

Não alterado, por já ser o padrão estabelecido no resto de `src/Tools/`
(verificado em 14 outros probes): a duplicação de `Teleportar`/`Verificar`/
`AvancarFase`/`Concluir`/`Procurar` entre `EnemyCrowdProbe.cs` e
`EnemyBrainProbe.cs` -- extrair uma base compartilhada agora tocaria em
todos os probes existentes por um ganho que não é deste ticket.

Depois das correções, `dotnet test` (337/337) e os probes afetados
(`EnemyBrainProbe`, `EnemyCrowdProbe`, `EnemyPoolProbe`,
`EnemyVisibilityProbe`, `AerialEnemyProbe`) foram re-executados -- inclusive
3 rodadas extras de `EnemyBrainProbe`/`EnemyCrowdProbe` para confirmar que a
correção do vazamento de `PhysicsRayQueryParameters3D` continua estável.
