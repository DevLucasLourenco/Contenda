# 30: O jogo abre num menu e o menu leva à partida

**What to build:** o jogo deixa de começar direto na arena. Abre num menu com a
cidade ao fundo, o jogador escolhe iniciar, vê os modos disponíveis, e entra na
partida sem a tela congelar durante o carregamento.

**Blocked by:** 28

**Status:** concluído

- [x] O menu principal oferece iniciar, configurações e sair
- [x] O fundo é a própria cidade em 3D, vista pela câmera do jogo
- [x] Escolher iniciar mostra os modos; só o modo horda é jogável
- [x] Os modos futuros aparecem visíveis e desabilitados, marcados como em breve
- [x] Trocar de tela não congela o jogo: há uma tela de carregamento com
      progresso real
- [x] Dá para navegar tudo por teclado e por mouse (**teclado verificado**: setas, Enter, ESC; mouse é `Button` padrão, sem clique simulado)
- [x] Voltar funciona em todas as telas (verificado no overlay de modos, a única tela com "voltar" hoje; o menu principal não tem para onde voltar)
- [x] **Sair de uma partida para o menu não deixa o menu pausado**
- [x] Navegar vinte vezes entre as telas não acumula nós esquecidos

## Comments

Mostrar os modos futuros desabilitados custa nada e comunica a ambição do
projeto — o jogador entende que horda é o começo, não o produto inteiro.

O critério do menu pausado parece bobo e é o bug clássico desta transição:
sair do pause para o menu leva junto o estado de pausa e a tela nasce morta.

## Implementação

### Telas

`MainMenu.tscn` (cena principal agora, em `project.godot`) tem o fundo 3D, o
painel INICIAR / CONFIGURAÇÕES / SAIR e, por cima, o overlay `GameModeMenu.tscn`
(Horde jogável; 1 VS 1, Boss Rush e Online desabilitados e marcados EM BREVE).
O overlay é instanciado e LIBERADO a cada visita -- nada fica esquecido na
árvore. ESC ou VOLTAR fecham o overlay e devolvem o foco a INICIAR.

**Fundo (`MenuBackdrop`)**: instancia `Arena.tscn` e remove, ANTES do `AddChild`
e por tipo, o jogador, os bonecos, o `CameraRig` e os diretores de onda -- um nó
removido antes de entrar na árvore nunca roda `_Ready`, então nada se anuncia ao
`GameSession`. Uma `Camera3D` própria orbita a cidade (150 s por volta). Conferi
o resultado num screenshot (`MenuCapture.tscn`, renderização de verdade).

**Carregamento**: `SceneRouter.GoToAsync` usa `ResourceLoader.LoadThreadedRequest`
e anuncia o progresso que o `ResourceLoader` reporta por
`GameEvents.SceneLoadProgress`; `LoadingScreen` (ao lado do HUD, adicionada pelo
`GameBootstrap`) só desenha esse evento. Toda transição despausa a árvore.

**Teclado**: foco inicial em INICIAR / no Horde; ESC = voltar; cartões e botões
desabilitados ficam fora do foco (`FocusMode = None`) -- por padrão o Godot deixa
a seta parar num botão desabilitado.

### Dois bugs achados no caminho (do ticket 29, não deste)

O HUD é um autoload e sobrevive à troca de cena, mas se ligava ao jogador UMA vez
e nunca soltava: depois de "tentar novamente" (ticket 29) ele ficava ligado a um
jogador já liberado, e a vida/mana/guia deixavam de atualizar. Agora o
`HudController` solta o jogador que saiu da árvore, some enquanto não há
jogador (menu, resultado) e liga ao novo. E o `CharacterController` só se
registrava em `GameSession.PlayerBody`, nunca se retirava -- agora tira a si
mesmo em `_ExitTree`.

### Verificação

`MenuProbe` (Enter no botão focado abre os modos e entra no Horde; o roteiro vive num "piloto" pendurado na raiz, que sobrevive às
trocas de cena que ele mesmo provoca): o menu abre com foco em INICIAR, câmera
de fundo ativa e em órbita, sem jogador e sem HUD; INICIAR abre 4 modos com só o
Horde habilitado e os outros marcados EM BREVE; seta para baixo do Horde pula os
desabilitados e cai em VOLTAR; ESC volta e libera o overlay; 20 idas e voltas não
mudam a contagem de nós; 4 voltas menu -> partida -> menu com a árvore PAUSADA de
propósito antes de cada troca (a última faz o caminho de verdade do jogador: morrer, tela de resultado, botão "menu principal"): a partida e o menu nascem despausados, o HUD liga
ao jogador NOVO e some no menu, `PlayerBody` fica nulo no menu, a tela de
carregamento aparece e o progresso nunca anda para trás, e a contagem de nós não
cresce entre a 2ª e a 3ª volta.

### O que não deu para verificar rigorosamente

- **Mouse**: os botões são `Button` padrão; o probe aciona por sinal e por ação de
  teclado, nunca por clique simulado.
- **A barra segue o progresso**: o probe confere os eventos (início, fim, ordem) e que a tela aparece,
  não que a barra desenhada acompanha cada valor.
- **Progresso intermediário**: a arena carrega rápido demais em headless para o
  `ResourceLoader` reportar valores entre 0 e 1 de forma garantida -- o probe prova
  início, fim, ordem e que a tela aparece, não uma barra "andando" em etapas.
- **Sair de verdade** (botão SAIR) e **configurações**: SAIR chama `Quit`, não
  testado (encerraria o probe); configurações é o ticket 32.
- A escolha de personagem (ticket 31) ainda não está no fluxo: HORDE vai direto à
  partida, com o personagem padrão da arena.
- "Menu principal" na tela de resultado agora está habilitado (o caminho existe)
  e usa o mesmo carregamento assíncrono; o `MenuProbe` o aperta de verdade.
- Ainda não há menu de pause (ticket 32): "sair do pause para o menu" só é
  coberto pela parte que existe (o roteador despausa toda transição).

### Code review

Duas sub-agents em paralelo (Standards e Spec) revisaram o diff staged. Achados
reais, corrigidos:

- **Membros mortos** (Standards): `SceneRouter.IsLoading` e
  `LoadingScreen.ShownProgress` não eram lidos por ninguém -- removidos.
- **Regra "em breve" duplicada em quatro lugares** (Standards): virou
  `ComingSoon.Apply` (desabilitado + dica + fora do foco do teclado).
- **Bloco de carregar/avisar/adicionar copiado três vezes** no `GameBootstrap`
  (Standards): `AdicionarNaRaiz` para a tela de resultado e a de carregamento (o
  HUD mantém a variante que falha alto em debug).
- **Caminho literal `GetNode("Fundo")` no probe** (Standards, convenções §2):
  `MainMenu.Backdrop` via `BackdropPath`.
- **O caminho real do jogador para o menu não era exercitado** (Spec): o probe
  ia direto pelo roteador. A última volta agora morre, espera a tela de
  resultado e aperta "menu principal" (que agora existe e está habilitado),
  conferindo despausado, tela escondida, sem jogador e HUD escondido.
- **Teclado só por setas** (Spec): a primeira entrada agora usa Enter no botão
  com foco (INICIAR, depois o Horde), provando foco + ação juntos.
- **Vazamento com só 2 voltas de comparação** (Spec): 4 voltas, e as três
  últimas precisam ter a mesma contagem de nós.

Aceito como está: `!` sem comentário no probe (padrão de todos os probes);
membros públicos lidos só pelo probe (`BoundPlayer`, `Cards`, botões --
precedente do `WaveBanner.IsShowing`); nenhum xUnit para o roteador/HUD
(dependem da engine; cobertos pelo `MenuProbe`); a câmera de fundo própria em
vez do `CameraRig` (decisão declarada); cartões de modo montados na cena, não
guiados por dado; as correções de `HudController`/`CharacterController` vêm do
ticket 29 mas só apareceram com o menu -- ficam neste commit, descritas acima.
