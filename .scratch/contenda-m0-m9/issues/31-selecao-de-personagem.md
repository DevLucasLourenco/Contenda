# 31: A escolha de personagem mostra a diferença entre os dois

**What to build:** antes de entrar na partida, o jogador vê os dois arquétipos
lado a lado — modelo em 3D, pontos fortes, as quatro sequências de habilidade e
a transformação — e o que ele escolher é quem entra na arena.

**Blocked by:** 30, 20

**Status:** concluído

- [x] Os dois aparecem lado a lado com preview em 3D de verdade, não imagem pronta
- [x] Barras comparam dano, alcance, velocidade e resistência
- [x] As quatro sequências de cada um aparecem, com os nomes das habilidades
- [x] A transformação de cada um é anunciada, com o que ela muda
- [x] O melhor resultado já obtido com aquele personagem aparece
- [x] Confirmar leva à arena com o personagem escolhido
- [x] **A tela não conhece os personagens:** ela é montada a partir dos dados, e
      um terceiro arquétipo apareceria sozinho

## Comments

O último critério é o teste real da arquitetura data-driven neste ponto do
projeto. Se adicionar um personagem exigir editar esta tela, o desenho vazou.

Mostrar as sequências aqui adianta o aprendizado: o jogador entra na partida já
tendo visto que `W W` mais o botão direito faz alguma coisa.

## Implementação

`roster.tres` define a ordem do elenco. Cada `CharacterDefinition` fornece modelo,
cores e ratings; habilidades e transformação vêm dos recursos já usados no jogo.
`CharacterSelectMenu` monta os cards em um contêiner rolável, com `SubViewport`
3D e movimento de idle leve, e mostra o recorde lido do perfil.

Confirmar grava `GameSession.SelectedCharacter`; o mesmo `CharacterController`
instancia o modelo escolhido na arena e configura todos os componentes com a
definição escolhida. A sonda `CharacterSelectProbe` carrega um elenco de teste
com terceira entrada e verifica card, preview, ficha, recorde e modelo
na arena. `MenuProbe` percorre o fluxo completo por teclado, incluindo VOLTAR.
