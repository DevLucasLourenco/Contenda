# 08: Ataque básico com espada encadeia três golpes

**What to build:** clicar o botão esquerdo com o espadachim dá um golpe de
espada. Clicar de novo dentro da janela encadeia o segundo, e depois o terceiro,
mais forte e com mais empurrão. Deixar a janela passar volta ao primeiro. Cada
golpe avança um pouco o personagem, para que acertar sob a câmera fixa seja
possível.

**Blocked by:** 07

**Status:** ready-for-agent

- [ ] O botão esquerdo dá um golpe que acerta o manequim
- [ ] Três cliques no ritmo encadeiam golpes crescentes; o terceiro empurra mais
- [ ] Perder a janela reinicia a sequência sem outra penalidade
- [ ] Cada golpe atinge cada alvo **uma única vez**
- [ ] A área de golpe só existe durante a janela ativa; nunca fica ligada
- [ ] Nenhum dano entre entidades do mesmo time
- [ ] Os números de dano e as janelas vêm de dados, não do código

## Comments

O botão esquerdo **não significa "soco"**: significa "ataque básico". A arma
equipada decide o que isso quer dizer. Essa abstração é o que permite o ticket
09 existir sem um único `if` sobre qual personagem está em jogo, e o que permite
o terceiro, o décimo e o quinquagésimo personagem depois. Ver spec 07, §1.

As janelas de acerto vivem em dados, **não** em faixas de chamada dentro da
animação. Motivo: no M8 os modelos e animações são substituídos, e faixas
embutidas se perderiam junto — ver spec 13, §6.

O bloqueio de ações durante o golpe é por fonte e com duração. Somar travas sem
origem produz o bug clássico de duas fontes travarem o movimento, uma liberar, e
o jogador destravar cedo demais.
