# 31: A escolha de personagem mostra a diferença entre os dois

**What to build:** antes de entrar na partida, o jogador vê os dois arquétipos
lado a lado — modelo em 3D, pontos fortes, as quatro sequências de habilidade e
a transformação — e o que ele escolher é quem entra na arena.

**Blocked by:** 30, 20

**Status:** ready-for-agent

- [ ] Os dois aparecem lado a lado com preview em 3D de verdade, não imagem pronta
- [ ] Barras comparam dano, alcance, velocidade e resistência
- [ ] As quatro sequências de cada um aparecem, com os nomes das habilidades
- [ ] A transformação de cada um é anunciada, com o que ela muda
- [ ] O melhor resultado já obtido com aquele personagem aparece
- [ ] Confirmar leva à arena com o personagem escolhido
- [ ] **A tela não conhece os personagens:** ela é montada a partir dos dados, e
      um terceiro arquétipo apareceria sozinho

## Comments

O último critério é o teste real da arquitetura data-driven neste ponto do
projeto. Se adicionar um personagem exigir editar esta tela, o desenho vazou.

Mostrar as sequências aqui adianta o aprendizado: o jogador entra na partida já
tendo visto que `W W` mais o botão direito faz alguma coisa.
