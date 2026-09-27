# 33: O jogo fica jogável por quem nunca o viu — fecha o MVP 0.1

**What to build:** o acabamento que transforma as peças soltas em jogo. HUD com
identidade visual coerente, tudo legível sobre a cidade, e um ciclo completo:
menu, escolha, partida, resultado, menu.

**Blocked by:** 32, 31, 29, 16

**Status:** ready-for-human

- [x] Um único tema visual governa toda a interface; nenhum estilo solto em tela
- [x] Vida, mana, guia de combos, seletor de forma, onda, tempo e placar
      convivem sem poluir a tela
- [ ] Tudo legível sobre a cidade, nos três níveis, em qualquer iluminação
- [x] Nada é comunicado **só** por cor
- [ ] A interface se adapta de 16:9 a 21:9 sem cortar informação
- [x] O ciclo menu → escolha → partida → resultado → menu fecha sem vazar nós
- [ ] **Um jogador que nunca viu o projeto consegue, sem ajuda:** iniciar,
      entender que WASD move e o botão esquerdo ataca, **executar uma habilidade
      de propósito em até dois minutos**, descobrir a transformação sozinho, e
      chegar à onda 2

## Comments

Este ticket fecha o **MVP 0.1**. Daqui em diante é arte, áudio e polimento — o
jogo já existe.

O último critério é o único que decide de verdade. Os outros são meios. Teste com
alguém de fora e observe sem falar nada; a tentação de explicar é a prova de que
a interface não está pronta.

### Implementação

- Tema global `assets/ui/theme/contenda.tres`; HUD e menus usam suas variações,
  sem overrides de estilo espalhados pelas cenas.
- HUD ancorado com margem de 3%, instruções acionáveis, estados textuais do guia,
  mana ligada à transformação, relógio e onda alimentados por eventos.
- Ciclo menu/partida validado sem crescimento de nós em `MenuProbe`.
- Validação: build `ExportRelease` sem avisos; 435 testes xUnit e sondas
  `HudProbe`, `ScoreAndResultsProbe`, `HordeMatchProbe`, `CharacterSelectProbe`
  e `MenuProbe` passaram.
- A revisão do diff observou que os probes headless não demonstram contraste em
  captura dos três níveis nem ausência de recorte de 16:9 a 21:9; esses dois
  critérios aguardam validação visual.
- O teste com uma pessoa de fora continua pendente; por isso o ticket aguarda
  revisão humana antes de fechar o MVP.
