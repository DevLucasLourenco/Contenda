# 26: Dá para saber quanto falta para cada inimigo cair

**What to build:** cada inimigo mostra a própria vida acima da cabeça, e os que
são mais perigosos se anunciam como tais. O chefe ganha uma barra própria no
alto da tela.

**Blocked by:** 25

**Status:** ready-for-agent

- [ ] A barra aparece sobre o inimigo só depois que ele apanha pela primeira vez
- [ ] Ela some sozinha algum tempo depois do último golpe
- [ ] A barra é legível sob a câmera fixa, em qualquer um dos três níveis da cidade
- [ ] Elites se distinguem de longe, e não só pela barra maior
- [ ] O chefe tem barra própria no alto da tela, com nome
- [ ] Com quarenta inimigos apanhando ao mesmo tempo, as barras não derrubam o
      desempenho
- [ ] **Nenhum nó 2D no mundo** — a vida sobre o inimigo é 3D ou projetada da
      câmera; `Control` só na interface

## Comments

Vida de inimigo no HUD central não faz sentido em modo horda: são muitos e o
jogador precisa saber de qual está perto de derrubar. Sobre a cabeça é onde o
olho já está.

O último critério existe porque este é o ticket mais tentador do projeto para
alguém resolver com um `Sprite2D` no mundo — que é reprovação de review.
