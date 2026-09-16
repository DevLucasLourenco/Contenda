# 25: Inimigos morrem, voltam ao pool e reaparecem íntegros

**What to build:** a base para dezenas de inimigos por onda sem engasgo. Ao
morrer, o inimigo toca a animação, some e volta para um estoque; ao ser
reaproveitado, volta **exatamente** como se tivesse acabado de nascer.

**Blocked by:** 22

**Status:** done

- [x] Morrer toca animação de morte e desliga a colisão antes de sumir
- [x] O inimigo morto volta para o estoque em vez de ser destruído
- [x] Reaproveitar um inimigo devolve vida cheia, sem atordoamento e sem
      modificadores de qualquer fonte
- [x] Nenhum aviso de evento fica pendurado de uma vida anterior
- [x] Nenhuma área de dano fica ativa órfã de um golpe interrompido pela morte
- [x] Reciclar o mesmo inimigo **cem vezes** deixa o estado idêntico ao do recém-criado
- [x] Uma onda inteira nascendo de uma vez não produz engasgo visível
- [x] O estoque é preparado antes da partida começar, não durante

## Comments

Vazamento de aviso de evento em objeto reciclado é o bug que este desenho mais
convida, e o mais difícil de achar depois: ele se manifesta como dano dobrado ou
morte instantânea vinte minutos depois, sem relação aparente com a causa.

O teste de cem ciclos não é exagero — é a única forma de pegar isso antes de
virar relato de jogador.

## Implementação

**`ICharacterComponent` ganhou um terceiro método, `ResetForSpawn()`,
obrigatório para todo mundo.** A maioria dos componentes já tinha o próprio
`ResetForSpawn()` implementado ad-hoc, de tickets anteriores que já
antecipavam este (`HealthState.Reset`/`HealthComponent.ResetForSpawn`,
`CombatComponent`, `StatsComponent`, `ManaComponent`, `AbilityComponent`,
`DamageFlashComponent`, `AttackTelegraphComponent`, `EnemyBrain` -- todos já
documentavam "Contrato do pool, no M5" nos próprios comentários). Faltavam só
`MovementComponent` (extraído um `ReiniciarEstadoTransiente()` privado,
compartilhado com `Configure`, para não duplicar a lista), `NavigationMotor`
e os dois componentes exclusivos do jogador (`TargetingComponent`,
`PlayerInputController`, vazios de propósito -- nunca reciclados, mas o
contrato vale para todo mundo, não só quem precisa dele hoje).
`CharacterController.ResetForSpawn()` é a única passada nova, iterando todos
os componentes -- ao contrário de `Bind`/`Configure`, não precisa de duas
passadas: nenhum componente depende de outro já resetado.

`WorldHealthBar` (não documentado em nenhum ticket anterior, mas já existente
na cena do grunt) passou a implementar `ICharacterComponent` só para entrar
nessa varredura e não ficar com a barra travada na fração de vida de uma
vida anterior -- já tinha o próprio `ResetForSpawn()` pronto, só não estava
plugado a nada.

**`EnemyState` ganhou `Death`.** `EnemyStateMachine.RegistrarMorte()`
transiciona para lá a partir de QUALQUER estado (inclusive `Airborne` --
um combo aéreo pode matar um inimigo ainda lançado) e nunca sai sozinho, só
por `ResetForSpawn` (uma máquina nova, em `Idle`). Defesa extra em
`RegistrarGolpeRecebido`: um golpe comum ou vertical contra quem já está
`Death` não faz nada -- `HealthState.Apply` já descarta todo golpe contra
quem não `IsAlive` (é por isso que `Died` "dispara no máximo uma vez por
vida"), então isto nunca deveria acontecer de verdade, mas a máquina não
deveria depender só disso.

**`EnemyBrain` reage a `Health.Died`** (mesmo padrão defensivo de
unsub-antes-de-sub que já protegia `Damaged`): `RegistrarMorte()`, cancela
telegraph/combate (a mesma chamada que já existia para interromper um golpe
ao atordoar -- "nenhuma área de dano órfã" sai de graça daí), e zera
`CollisionLayer`/`CollisionMask` do corpo IMEDIATAMENTE, não só ao devolver
ao pool: o corpo fica visível (o placeholder de "animação de morte" --
`EnemyDefinition.DeathDuration`, 1.2 s por padrão, sem modelo/`AnimationPlayer`
de verdade ainda, ADR-010) mas não deveria continuar bloqueando passagem
nem contando como alvo enquanto isso. `Poll()` passa a checar `Estado ==
Death` primeiro: enquanto morto, só conta o próprio temporizador e devolve
`IntentFrame.Idle`, sem tocar percepção/ataque nenhum.

**`EnemyPool`** (novo autoload, `src/Components/AI/EnemyPool.cs`) prewarma
60 grunts no boot (dimensionamento da spec 09 §8), do mesmo jeito que
`ProjectilePool`/`DamageNumberPool` já prewarmam -- "o estoque é preparado
antes da partida começar" já vale trivialmente assim, mesmo sem sistema de
onda nenhum ainda (ticket 27). Nunca chama `Instantiate`/`QueueFree` fora do
prewarm (ADR-009): "morrer" só desativa (esconde, zera colisão,
`ProcessMode.Disabled`) e empurra numa pilha livre por espécie; `Acquire`
resolve dessa pilha, cresce além do prewarm só como último recurso (com
aviso, não silencioso). Ao contrário de `ProjectilePool` (dados puros, sem
nó por disparo), um inimigo é uma árvore inteira que quem chama precisa
referenciar de volta, então `Acquire` é uma chamada direta que devolve o
próprio `CharacterController` -- não um evento no `GameEvents`, que só
transporta dados por valor, nunca nós (regra explícita já documentada ali:
"um evento que carrega um nó mantém vivo algo que o pool achou que tinha
reciclado"). `Release`, por outro lado, É acionado pelo próprio `EnemyBrain`
ao fim da morte -- como não pode ir pelo barramento global, `EnemyBrain`
guarda uma referência direta ao pool que o criou, entregue via um novo campo
em `CharacterContext` (`EnemyBrain`, adicionado só para o pool alcançar o
cérebro de um inimigo recém-instanciado sem `GetNode` por caminho).

**Duas corridas de física reais, achadas rodando o probe, não só
calibração:**

1. Um `CharacterBody3D` instanciado no PRÓPRIO `_Ready` de um autoload --
   antes da árvore ter rodado seu primeiro `_PhysicsProcess` -- ainda não
   tem espaço físico atribuído pelo servidor; um `MoveAndSlide` nesse
   instante falha com `"body->get_space() is null"`. `ProjectilePool`/
   `DamageNumberPool` nunca bateram nisto por só criarem `MeshInstance3D`,
   sem corpo físico nenhum. Corrigido adiando o prewarm inteiro com
   `CallDeferred`, mesmo remédio que `WorldHealthBar.Atualizar` já usa para
   uma defasagem parecida -- e uma property nova, `EnemyPool.IsReady`, para
   quem depende do prewarm (o próprio probe) esperar pelo sinal de verdade
   em vez de chutar quantos quadros isso leva.
2. Mais sutil: `EnemyBrain.Poll()` é só o PASSO 1 do `_PhysicsProcess` do
   contêiner (spec 01 §6) -- `Movement.Tick` (passo 3) ainda roda DEPOIS, no
   MESMO quadro. Chamar `Pool.Release` direto de dentro de `Poll()` (ao fim
   do temporizador de morte) zera a colisão e desliga `ProcessMode` ainda
   dentro do passo 1 -- e o `MoveAndSlide` do passo 3, alguns microssegundos
   depois, encontra um corpo que acabou de perder o espaço físico, mesmo
   erro `"body->get_space() is null"` de novo, por um motivo totalmente
   diferente do primeiro. Corrigido chamando `Pool.Release` via
   `CallDeferred`, para rodar só depois do quadro inteiro (todos os passos,
   de todos os personagens) terminar.

**Bug real de reset, achado pelo probe, corrigido em `MovementComponent`:**
`ResetForSpawn`/`Configure` zeravam `IsGrounded` mas não a property `Velocity`
(um espelho de `corpo.Velocity` só atualizado no PRÓXIMO `Tick`) nem
`corpo.Velocity` em si -- um inimigo reciclado herdaria o impulso físico de
um golpe da vida anterior por um quadro inteiro. Corrigido zerando os dois
dentro do `ReiniciarEstadoTransiente()` compartilhado.

**Probe novo:** `src/Tools/EnemyPoolProbe.cs` +
`scenes/debug/EnemyPoolProbe.tscn`. Quatro fases: ciclo natural (mata de
verdade via dano+atordoamento primeiro, depois `Health.Kill`, confirma
colisão desligada e corpo ainda visível durante a janela de morte, e que
volta ao pool SOZINHO depois de `DeathDuration`, sem ninguém chamar `Release`
à mão); reaquisição limpa (confirma vida cheia, sem i-frames, chão
confiável, sem ataque em andamento, colisão restaurada); cem ciclos
(`ResetForSpawn` direto na MESMA instância, cem vezes seguidas, sujando de
verdade entre cada um -- o requisito literal do ticket); e rajada de onda
(`Acquire` de TODO o resto do estoque prewarmado -- não um número arbitrário
menor -- no mesmo quadro, confirmando que uma onda do tamanho do próprio
dimensionamento sai inteira sem cair no caminho que instancia em runtime).
Todas as posições de teste ficam bem além do `LoseTargetRadius` do "Jogador"
de verdade que a Arena já inclui (a mesma corrida "Idle vira Alert perto de
um alvo visível" já resolvida no ticket 24), mas ainda em cima do chão de
60×60 m -- não flutuando no vazio, onde "assentado no chão" nunca seria
verdade.

### Code review

Duas revisões em paralelo (Standards e Spec) sobre o diff staged. Achados
reais, corrigidos:

- **Standards + Spec, convergindo:** `EnemyPool.Prewarm`/`Acquire` recebem
  `PackedScene` explícita, desviando do esboço de `EnemyDefinition`-só da
  spec 09 §8 (que pressupõe `ModelScene`, ainda não implementado). Deixar
  spec e código divergindo é o mesmo tipo de achado que o ticket 24 já
  reprovou — corrigido atualizando a spec para o formato real, com a
  justificativa (`ModelScene` pertence aos tickets 26/29/34), em vez de
  inventar `ModelScene` agora por pressão do documento (o que seria
  desenhar para necessidade hipotética, regra do CLAUDE.md).
- **Spec, item 1 do contrato de reciclagem:** "desassina todos os eventos"
  não acontece literalmente no `Release` — `Bind`/`Configure` nunca rodam de
  novo num nó pooled, então cada assinatura de evento é feita uma vez só, no
  nascimento, e dura a vida inteira do nó. Avaliado e considerado correto
  (não um gap): o handler continua vivo e correto porque reage ao ESTADO
  atual, já zerado por `ResetForSpawn`, não a um resquício da vida anterior.
  Documentado explicitamente na spec 09 §8 em vez de reescrever para
  desassinar/reassinar a cada ciclo, o que exigiria reintroduzir `Bind` no
  caminho de `Acquire` sem necessidade real.
- **Spec, "onda inteira sem engasgo":** a rajada do probe pedia só 30
  grunts, bem menos que os 60 do dimensionamento, então nunca provava que o
  estoque INTEIRO sai sem cair no caminho de crescimento além do prewarm
  (`CriarInstancia`, que instancia em runtime -- exatamente o engasgo que
  o ticket pede para evitar). Corrigido: a fase agora pede o TAMANHO
  EXATO do que resta do estoque (`GruntPoolSize - ActiveCount`), provando o
  caso real.
- **Standards, Duplicated Code:** `if (Estado == EnemyState.Death) return;`
  repetido em `RegistrarMorte` e `RegistrarGolpeRecebido`. Extraída uma
  property privada `EstaMorto` nos dois lugares.
- **Standards, Data Clump:** `EnemyPool.Especie` guardava
  `CamadaOriginal`/`MascaraOriginal` como dois dicionários paralelos, sempre
  lidos/escritos juntos. Combinados num só, `ColisaoOriginalPorInstancia`,
  com um `record struct ColisaoOriginal(uint Camada, uint Mascara)`.
- **Standards, julgamento (sem mudança):** `WorldHealthBar` implementar
  `ICharacterComponent` só com `Bind`/`Configure` vazios (Refused Bequest) e
  o `EnemyPool` ter campos específicos do grunt (`GruntScenePath` etc., em
  vez de um roster genérico) foram sinalizados como aceitáveis pelo próprio
  revisor -- o primeiro porque o contrato já sanciona implementações vazias
  para quem não precisa, o segundo porque generalizar para espécies que
  ainda não existem em código (runner/shooter/brute/warlord, tickets
  futuros) seria a mesma generalidade especulativa que `EnemyDefinition.cs`
  já evita explicitamente hoje. Nenhuma mudança feita.

Gate completo re-executado após as correções: 325/325 testes, 14/14 probes
headless (incluindo `EnemyPoolProbe` 3x seguidas), `ExportRelease` e
`check-no-2d.sh` limpos.
