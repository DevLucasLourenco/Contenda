# 16: A interface ensina as combinações e mostra o que o jogo entendeu

**What to build:** uma lista permanente das habilidades com suas sequências, que
**reage enquanto o jogador digita** — acendendo o que já foi digitado, apagando
o que não casa mais, e destacando a linha pronta para confirmar.

**Blocked by:** 14, 12

**Status:** done (critério de playtest humano pendente — ver Implementação)

- [x] A lista aparece sozinha a partir dos dados do personagem; trocar de
      personagem troca a lista inteira
- [x] Digitar o primeiro símbolo acende a parte correspondente e apaga as linhas
      impossíveis
- [x] Completar uma sequência destaca a linha e sinaliza que basta confirmar
- [x] Habilidade em recarga aparece esmaecida com o tempo restante — mas
      **continua listada**, porque o jogador precisa decorar a sequência
- [x] Habilidade sem mana mostra o custo em vermelho
- [x] Confirmar sem casamento faz a lista piscar
- [ ] Um jogador que nunca viu o jogo executa uma habilidade **de propósito** em
      menos de dois minutos, sem explicação externa

## Comments

Este é o ticket que decide se o sistema de comandos é aprendível. Sem ele, a
mecânica parece quebrada: o jogador digita e nada acontece, sem saber por quê.

O último critério é o único que importa de verdade. Teste com alguém de fora.

## Implementação

`AbilityGuide` (`src/UI/HUD/AbilityGuide.cs`), um `Control` novo no `Hud.tscn`
ao lado das barras do ticket 12. Uma linha por habilidade (texto puro, sem
ícone — mesma filosofia de placeholder das barras, arte de verdade só no
ticket 34+), reconstruída do zero a cada `AbilityComponent.AbilitiesChanged`
(evento novo, disparado no fim de `Configure`) — cobre tanto o boot quanto a
troca de arquétipo em runtime (tecla de debug do ticket 12).

`AbilityMatchEvaluator` (POCO, testado em xUnit) decide o estado de cada linha
a partir só de duas coisas: a sequência da habilidade e o buffer digitado
agora (`AbilityComponent.CurrentSequence`) — `Neutral`/`PartialMatch`/
`Impossible`/`Complete`. "Acende"/"apaga"/"destaca" viram a cor da linha
inteira, não por símbolo: sem ícone por letra ainda, granularidade de linha já
cobre os critérios de aceite sem inventar um sistema de símbolos que a arte
ainda não tem. Recarga esmaece a opacidade da linha (mostrando o tempo
restante no texto) sem nunca escondê-la; mana insuficiente colore só o número
do custo em vermelho via BBCode. `SequenceRejected` pisca a lista inteira por
`RejectFlashDuration`, sobrepondo cor E opacidade — do contrário o aviso
passaria despercebido bem na habilidade que acabou de entrar em recarga, o
caso mais comum de gerar uma rejeição.

Verificado por `HudProbe` (headless, árvore real, `Arena.tscn`): a lista
aparece com as 4 habilidades do Swordsman, W acende Dash Slash/Heavy Lunge e
apaga Rising Slash, W W completa Dash Slash e derruba Heavy Lunge, confirmar
esmaece a linha na hora (recarga começa no início — ticket 14), S S sem
match nenhum pisca a lista, e a troca de arquétipo (tecla de debug) reconstrói
para as 4 habilidades da Gunslinger com o buffer zerado.

**Não verificado por este agente**: o último critério de aceite pede um
playtest com uma pessoa que nunca viu o jogo, executando uma habilidade de
propósito em menos de 2 minutos sem explicação externa — isso exige um
humano de verdade, não é algo que um probe automatizado possa substituir.
Falta esse playtest antes de fechar o ticket com confiança total nesse
critério específico.

### Code review

Achado real: a cor de ESTADO da linha vivia em `Modulate`, que multiplica tudo
que a linha desenha — o vermelho do custo ficava manchado (ciano × vermelho
dá um marrom, não vermelho) sempre que o estado não era neutro, justo nos
momentos (digitando, prefixo impossível) em que o jogador mais está olhando
para o custo. Corrigido: cor de estado e cor do custo agora são tags de
BBCode aninhadas no próprio texto (a de dentro nunca é afetada pela de fora);
`Modulate` sobrou só para opacidade (recarga), que nunca desloca matiz.

Também corrigido: `AtualizarLinha` reconstruía o BBCode inteiro (alocação +
reparse do `RichTextLabel`) a cada quadro de física, mesmo parado — agora só
reconstrói quando o estado exibido de fato muda (comparação por valor contra
um cache por linha, arredondando o cooldown à mesma casa decimal mostrada no
texto). E o símbolo de uma `CommandDirection` sem mapeamento falha alto em
debug em vez de cair silenciosamente num "?" — mesmo padrão da
`WeaponFactory`/`AbilityBehaviorRegistry`.
