# 17: O personagem pula e avança com dash

**What to build:** as duas ações de mobilidade que sustentam todo o resto do
desenho novo. Espaço pula com a tolerância que faz o pulo parecer justo; Shift
avança rápido, com um instante de invulnerabilidade que serve de escape.

**Blocked by:** 05

**Status:** done

- [x] Espaço pula até uma altura configurada **em metros**, não em velocidade
- [x] Pular logo depois de sair de uma borda ainda funciona
- [x] Apertar Espaço pouco antes de aterrissar pula assim que toca o chão
- [x] A queda é mais pesada que a subida — o pulo não flutua
- [x] O controle no ar é reduzido, mas existe
- [x] Shift avança na direção do movimento, ou na que o personagem encara se
      estiver parado
- [x] Durante o dash não dá para curvar
- [x] O dash concede invulnerabilidade breve e **serve para escapar de atordoamento**
      (invulnerabilidade + não é bloqueado por `ActionLock.Movement` — ver
      Implementação sobre o atordoamento em si ainda não existir)
- [x] O dash **não consome mana**, só recarga
- [x] Dá para usar o dash uma vez por pulo, no ar
- [x] A câmera **não balança a cada pulo**, mas acompanha quando o jogador muda
      de patamar

## Comments

O último critério é o que costuma ser esquecido e o que mais incomoda: seguir a
altura do jogador com a mesma suavização usada em X e Z faz a câmera pular junto,
e sob ângulo fixo isso enjoa em minutos. Altura precisa de suavização própria,
muito mais lenta.

O dash não custar mana é decisão de desenho, não esquecimento — ver
[spec 16 §4](../../../docs/specs/16-mobilidade-criticos-e-combate-aereo.md).

## Implementação

`JumpState` e `DashState` (POCOs novos em `src/Components/Movement/`, testados
em xUnit) guardam coyote time/jump buffer e o avanço de velocidade constante do
dash, respectivamente — mesma disciplina do `LungeMotion`/`KnockbackState` já
existentes: janela de tempo é onde um erro de comparação passa despercebido, e
testar isso exige controlar o relógio fora da engine.

`MovementMath.ApplyGravity` ganhou `fallGravityScale` (só multiplica a
gravidade quando já caindo, nunca na subida) e `MovementMath.JumpVelocity`
deriva a velocidade de saída de `JumpHeight` — nenhuma velocidade abstrata em
lugar nenhum do `.tres`.

`HealthComponent` ganhou uma SEGUNDA fonte de invulnerabilidade
(`GrantInvulnerability`), somada por OR com a pós-golpe já existente — as duas
nunca se cortam mutuamente, mesma disciplina do `ActionLockSet`.

O dash trava só `ActionLock.Rotation` (nunca `Movement`, que pertenceria à
decisão de ignorar WASD) e só é bloqueado por `ActionLock.Abilities` — nunca
por `Movement` — porque a spec exige que ele escape de recovery de ataque
básico e de hitstun (ambos travam Movement) sem escapar de uma habilidade em
execução (que trava Abilities). **Atordoamento pesado como sistema ainda não
existe** (chega com inimigos, M5) — o dash já está pronto para servir de
escape assim que existir, sem precisar de nenhuma mudança aqui.

A câmera já tinha `VerticalFollowSmoothTime`/`VerticalDeadZone` desde a spec de
câmera (pré-existente a este ticket) — este ticket só comprova, via probe, que
os valores padrão (zona morta de 2,4 m vs. altura de pulo de 2,2 m) realmente
absorvem um pulo sem a câmera se mexer.

Achado real de bug durante a escrita do probe (não do código de produção
final, mas quase): a checagem de "já usei o dash neste pulo" usava
`IsOnFloor()` do próprio quadro, que ainda reflete o chão de ANTES da física
deste quadro rodar — no quadro exato em que um pulo dispara, essa leitura
ainda vem "apoiado", e um dash pedido nesse instante nunca marcava a bandeira.
Corrigido somando a velocidade vertical já decidida (pulo ou queda) ao sinal
de "realmente no ar", em vez de confiar só em `IsOnFloor()`.

Verificado por `MovementProbe` (headless, árvore real, reaproveitando a
Plataforma de `Arena.tscn` como borda para o teste de coyote): altura do
pulo, coyote time, jump buffer, queda mais pesada que a subida (subida vs.
descida da MESMA altura em menos quadros), controle aéreo reduzido mas
existente, distância e trava de rotação do dash, invulnerabilidade bloqueando
um golpe de verdade, mana intacta, e a regra de um dash por pulo no ar
(incluindo o aterrissar liberando de novo).

### Code review

Dois achados de spec aplicados: cobertura de borda exata faltando em
`JumpStateTests` (só havia teste com folga de vários quadros para coyote time
e jump buffer expirarem, nenhum no quadro EXATO em que a janela zera) —
adicionados `Coyote_time_nao_pula_mais_exatamente_no_quadro_em_que_a_janela_esgota`
e `Jump_buffer_nao_vale_mais_exatamente_no_quadro_em_que_a_janela_esgota`. E a
margem `velocidade.Y > 0.1f` usada para decidir "realmente no ar" (ver bug do
`noChao` acima) trocada por `> 0f` com comentário explicando por quê: os
únicos dois valores que essa variável assume ali são o repouso do
`ApplyGravity` (-1) ou uma saída de pulo (sempre vários m/s) — não há ruído
de ponto flutuante ali que justifique tolerância.

Um achado de standards investigado e descartado: o reviewer apontou a
posição da constante `DashLockSource` (logo após o bloco `[Export]`) como
fora de lugar; `CombatComponent.SelfLockSource` já vive na mesma posição
relativa — é o padrão existente do arquivo, não um desvio, então não foi
movida.

Regressão completa (259 testes xUnit, os 8 probes headless pré-existentes,
`MovementProbe`, `ExportRelease` e o checador anti-2D) reexecutada depois dos
ajustes de review; tudo verde.
