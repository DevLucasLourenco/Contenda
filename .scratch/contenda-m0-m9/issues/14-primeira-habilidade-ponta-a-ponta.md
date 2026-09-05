# 14: Uma sequência confirmada executa uma habilidade e cobra mana

**What to build:** o momento em que a ideia central do jogo existe pela primeira
vez. Digitar a sequência, confirmar com o botão direito, e o espadachim avança
cortando — descontando mana e entrando em recarga.

**Blocked by:** 13, 10, 08

**Status:** done

- [x] A sequência certa mais a confirmação executa a habilidade sem atraso perceptível
- [x] A mana é descontada **só depois** de todas as verificações passarem
- [x] Sem mana suficiente: aviso claro, e nada é consumido
- [x] Em recarga: aviso claro, com o tempo restante disponível para a interface
- [x] Confirmar sem sequência válida limpa a fila e sinaliza a falha —
      **nunca** cai no ataque básico como consolo
- [x] A habilidade é descrita por um arquivo de dados: custo, recarga, dano,
      alcance e animação saem de lá, não do código
- [x] Morrer durante a execução cancela sem deixar área de dano ativa órfã
- [x] A recarga começa no **início** da execução, não no fim

## Comments

Fim deste ticket é o **go/no-go mais importante do projeto**: executar
habilidades por sequência é divertido ou é um obstáculo?

Se for obstáculo, o plano B já está desenhado — teclas numéricas disparam as
habilidades e a sequência vira atalho opcional com bônus. O arquivo de dados não
muda; muda só a origem do gatilho. Decidir **aqui**, não no M7.

## Implementação

`AbilityDefinition` (Resource, spec 05 §1), `AbilityContext`/`IAbilityBehavior`/
`AbilityBehaviorRegistry` (spec 05 §3) e `AbilityComponent` (spec 05 §2) chegam
todos juntos — só `DashAttackBehavior` (Dash Slash) tem efeito implementado; as
outras 7 habilidades do catálogo do MVP ficam para o ticket 15, cada uma
falhando alto em debug/logando em release se um `.tres` pedir um `Kind` sem
comportamento ainda, mesmo desenho da `WeaponFactory`.

A ordem de gates (`AbilityGateEvaluator`) e o relógio de cast+recovery
(`AbilityTimeline`) são POCOs extraídos à parte, testados em xUnit sem tocar
Godot — mesma disciplina do `MeleeCombo`/`KnockbackState`: é exatamente esse
tipo de lógica (ordem de validação, "recarga começa no início") que regride em
silêncio dentro de um método grande de `Node`.

`PlayerInputController` grava W/A/S/D na borda de subida como símbolos de
comando (campos novos em `IntentFrame`, distintos do eixo composto `Move`);
M2 (`command_confirm`, já existente) confirma. Wiring novo em
`CharacterController._PhysicsProcess`, entre combate e mana.

Não fica pronto no ticket: gate de tags (`RequiredTags`/`BlockedByTags` — sem
fonte de tag real até o M4), HUD de combo/cooldown (ticket 16), as outras 7
habilidades (ticket 15).

Verificado por `AbilityCooldownTrackerTests`, `AbilityGateEvaluatorTests`,
`AbilityTimelineTests` (xUnit) e `AbilityProbe`/`AbilityArena` (headless, árvore
real): sequência+confirmação executa e desconta mana atomicamente, recarga
bloqueia reexecução com `CooldownRemaining` positivo, mana insuficiente não
consome nada, sequência sem match nunca cai no ataque básico, e morrer a meio
do cast cancela sem aplicar dano.
