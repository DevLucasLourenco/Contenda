# 14: Uma sequência confirmada executa uma habilidade e cobra mana

**What to build:** o momento em que a ideia central do jogo existe pela primeira
vez. Digitar a sequência, confirmar com o botão direito, e o espadachim avança
cortando — descontando mana e entrando em recarga.

**Blocked by:** 13, 10, 08

**Status:** ready-for-agent

- [ ] A sequência certa mais a confirmação executa a habilidade sem atraso perceptível
- [ ] A mana é descontada **só depois** de todas as verificações passarem
- [ ] Sem mana suficiente: aviso claro, e nada é consumido
- [ ] Em recarga: aviso claro, com o tempo restante disponível para a interface
- [ ] Confirmar sem sequência válida limpa a fila e sinaliza a falha —
      **nunca** cai no ataque básico como consolo
- [ ] A habilidade é descrita por um arquivo de dados: custo, recarga, dano,
      alcance e animação saem de lá, não do código
- [ ] Morrer durante a execução cancela sem deixar área de dano ativa órfã
- [ ] A recarga começa no **início** da execução, não no fim

## Comments

Fim deste ticket é o **go/no-go mais importante do projeto**: executar
habilidades por sequência é divertido ou é um obstáculo?

Se for obstáculo, o plano B já está desenhado — teclas numéricas disparam as
habilidades e a sequência vira atalho opcional com bônus. O arquivo de dados não
muda; muda só a origem do gatilho. Decidir **aqui**, não no M7.
