# 32: O jogo pausa, e as configurações sobrevivem a fechar o jogo

**What to build:** apertar Esc no meio da luta congela tudo e abre um menu. Dali
dá para ajustar vídeo, áudio, teclas e opções de jogo — e essas escolhas
continuam valendo na próxima vez que o jogo abrir.

**Blocked by:** 30

**Status:** concluído

- [x] Esc congela a partida e abre continuar, configurações, reiniciar e sair
- [x] O HUD continua visível atrás do menu — o jogador pausa justamente para ler
      as combinações
- [x] Vídeo: modo de janela, resolução, sincronização, limite de quadros, sombras
      e escala de renderização
- [x] Áudio: volumes separados, todos audivelmente diferentes ao mexer
- [x] Teclas: **todas** as ações remapeáveis, com aviso de conflito e opção de
      restaurar padrões
- [x] Jogo: intensidade do tremor de tela, números de dano, guia de combos e a
      **tolerância da janela de comandos**
- [x] Fechar e reabrir o jogo preserva tudo
- [x] Apagar o arquivo de configurações faz o jogo abrir com os padrões, sem quebrar
- [x] As configurações são as mesmas pelo menu principal e pelo pause

## Comments

A tolerância da janela de comandos como opção é acessibilidade deliberada: o
sistema de sequências é a barreira de entrada do jogo, e a tolerância de tempo
varia muito entre jogadores. Quem não consegue executar não joga.

Remapear a tecla de confirmar precisa refletir na guia de combos, senão o HUD
passa a mentir.

## Implementação

### Dado puro, testado em xUnit (`src/Settings/`)

`GameSettings` (vídeo, áudio, jogo, teclas trocadas), `SettingsSerializer` (o
INI de `user://settings.cfg`, spec 14 §2) e `BindingSpec` (uma tecla como
"key:87"/"mouse:1", o padrão com as trocas do jogador por cima, e a conta de
conflito). Leitura tolerante: valor ilegível cai no padrão, número fora da faixa
é puxado para dentro, `version` desconhecida devolve os padrões, arquivo vazio
ou apagado dá os padrões. `GameSettings.Bindings` guarda só o que o jogador
MUDOU -- uma ação nova em uma versão futura nasce com o padrão, sem migração.
`SettingsSerializerTests` + `BindingSpecTests` (23).

A janela da tolerância dos comandos é a acessibilidade da spec: curta 0,5 s,
normal 0,7 s (o padrão de antes), longa 0,9 s.

### Engine (`SettingsStore`, `GameSession.Settings`)

`SettingsStore` lê/grava (atômico, o mesmo `AtomicFile` do perfil) e aplica: janela
(modo, resolução), vsync, limite de quadros, tamanho do atlas de sombras,
`Scaling3DScale`, os cinco buses de áudio (Master/Music/SFX/UI/Ambience, criados
se faltarem) e o `InputMap`. Roda no boot (`GameBootstrap`), antes da primeira
cena. `Commit` = guarda em `GameSession.Settings`, grava, aplica e avisa por
`GameEvents.SettingsChanged`; **salvar acontece ao aplicar**, não a cada slider.

Quem depende das opções relê pelo evento: `CameraRig` (tremor x a opção),
`DamageNumberPool` (não cria número com a opção desligada), `AbilityComponent`
(recria o buffer com a janela escolhida) e `AbilityGuide` (esconde/mostra e
**mostra "+ tecla" depois de cada sequência, lida do `InputMap`** -- então
remapear a confirmação nunca deixa a guia mentindo; antes ela não mostrava
tecla nenhuma).

### Telas

`SettingsMenu.tscn` -- UMA cena, aberta por cima do menu principal e do pause --
com abas Vídeo / Áudio / Controles / Jogo montadas em código a partir dos dados.
Edita um RASCUNHO: VOLTAR descarta, APLICAR grava e aplica. A aba Controles lista
`SettingsStore.RemappableActions()` (as ações do `InputMap` menos `ui_*`, `pause` e
as de debug), então uma ação nova aparece sozinha. Remapear: clica, aperta a
tecla; se outra ação já a usa, avisa quem perde e pede para repetir (ela fica sem
tecla); ESC cancela a captura; RESTAURAR PADRÕES.

`PauseMenu.tscn` (ao lado do HUD, `ProcessMode = Always`): Esc pausa a árvore e
abre CONTINUAR / CONFIGURAÇÕES / REINICIAR / SAIR PARA O MENU; o HUD fica
visível atrás. Só abre com uma partida acontecendo (jogador vivo, sem resultado
pendente -- `HordeGameMode` limpa `LastResult` ao começar). Sair passa por
`SceneRouter.GoToAsync`, que despausa.

### Verificação

- `SettingsProbe`: o menu tem 4 abas e um botão por ação remapeável (sem
  `ui_*`/pause/debug); aplicar muda o dB de cada bus de forma independente (0 =
  mudo); desligar números de dano / guia, janela curta/longa e tremor 0% chegam
  ao jogo e ligar de novo desfaz; rebind entra no `InputMap` SUBSTITUINDO a tecla
  antiga; remapear a confirmação muda o texto da guia; conflito avisa e só aplica
  ao repetir; o arquivo grava e "reabrir" traz tudo de volta; apagar o arquivo dá
  os padrões e restaurar padrões devolve as teclas; arquivo corrompido não quebra.
  Rodado também COM janela: tamanho 1366x768 e vsync desligado conferidos de
  verdade no `DisplayServer`.
- `MenuProbe`: CONFIGURAÇÕES do menu principal abre e ESC fecha; Esc no meio da
  partida pausa, o HUD segue visível, Esc continua; CONFIGURAÇÕES no pause abre
  e ESC volta ao pause (não à partida); SAIR PARA O MENU leva ao menu
  despausado; o pause não abre no menu; e o pause e o menu principal usam a
  MESMA cena de configurações.

### Um problema achado no caminho (dos tickets 29/30)

Os probes que jogam uma partida até o fim (o `MenuProbe`, ao morrer) gravavam no
`user://profile.cfg` de VERDADE -- o mesmo problema que o `ScoreAndResultsProbe`
já evitava com um caminho próprio, só que por uma exportação do
`HordeGameMode`, inacessível a um probe que carrega a partida por cena. Agora o
caminho é `GameSession.ProfilePath` (como `SettingsPath`) e todo probe de
partida aponta para o seu. (As 11 partidas falsas que já tinham sido gravadas no
perfil de teste desta máquina foram apagadas.)

### O que não deu para verificar rigorosamente

- **Volumes "audivelmente diferentes"**: não há som no jogo ainda (o
  `AudioDirector` é vazio) e o `AudioServer` do headless é mudo. Confere-se o dB
  de cada bus, não o que se ouve.
- **Vídeo aplicado no headless**: sem `DisplayServer` de verdade, modo de janela
  e vsync só foram conferidos numa execução COM janela (tamanho e vsync); tela
  cheia / sem borda e as sombras/escala não foram verificadas visualmente.
- **Nem tudo da spec 14 §1 está feito**: sem SSAO/SSIL, anti-aliasing, "silenciar
  em segundo plano", sensibilidade do mouse, inverter scroll, idioma e
  dificuldade (o ticket 32 não os lista). A lista de resoluções é fixa
  (1280x720 a 3840x2160) -- o Godot 4 não tem `ScreenGetModeList`.
- **Mouse**: os botões são `Button` padrão; o probe aciona por sinal e por ações
  de teclado, e a captura de tecla do remapeamento é exercitada pelo caminho de
  proposta (`OfferBinding`), não por um `InputEvent` real chegando em `_Input`.
- O "blur" do fundo do pause (spec 11 §3.5) não foi feito.

### Code review

Duas sub-agents em paralelo (Standards e Spec) revisaram o diff staged. Achados
reais, corrigidos:

- **Abrir o jogo "de verdade" não era exercitado** (Spec): o probe chamava
  `Load` + `Apply` na mão. O que o `GameBootstrap` faz virou
  `SettingsStore.LoadAndApply(sessao, tree)`, e o `SettingsProbe` a chama sobre
  um arquivo gravado depois de voltar o InputMap aos padrões, conferindo teclas,
  volume e a sessão.
- **Captura de tecla só pelo caminho de proposta** (Spec): o probe agora clica no
  botão e injeta `InputEvent`s reais em `_Input` -- roda do mouse é ignorada, ESC
  cancela sem mudar a tecla, uma tecla vira a ação no rascunho e no botão.
- **Trocar uma tecla apagava o resto do padrão da ação** (Spec): um evento que o
  menu não sabe remapear (um botão de controle) era perdido na primeira
  aplicação. Agora é preservado.
- **`!` sem comentário e estado estático escondido** (Standards): `_padroes!`
  virou um acessor `Padroes()` que documenta por que é estático (o `InputMap` é
  global da engine; guarda-se o que havia ANTES da primeira troca); o `_abas!`
  do menu virou um `throw` explícito. `"pause"` literal agora usa
  `InputActions.Pause`; guardas de nulo em `Load`/`Describe`/`For`.
- **Membros públicos sem leitor** (Standards): `PauseMenu.ContinueButton` e
  `RestartButton` agora são exercitados de verdade pelo `MenuProbe` (CONTINUAR
  volta à partida; REINICIAR recarrega a cena, com jogador novo e despausado).

Aceito como está:

- **`pause` não é remapeável**: a spec 14 §1 diz "exceto `pause` e `ui_*`" -- não
  é uma lacuna do "todas as ações". Uma tecla ligada a Enter/Espaço não avisa
  conflito com `ui_accept`: as ações de interface valem em outro contexto
  (`jump` já é Espaço por padrão).
- **Os tempos 0,5/0,7/0,9 s e os tamanhos de sombra em `.cs`**: são o mapa
  opção -> valor que a própria spec 14 fixa, não balanceamento; os rótulos da
  tela repetem os números (um lugar a mais a mudar se a spec mudar).
- **`GameSettings.Clone/Normalize/Serialize/Parse` enumeram os campos à mão**:
  um campo novo mexe em ~6 lugares. Aceito até haver mais campos que valham um
  mecanismo genérico.
- **Abrir/fechar o overlay de configurações duplicado** em `MainMenu` e
  `PauseMenu` (dois hospedeiros, dois ciclos de vida).
- **Pause de verdade só provado por `Tree.Paused` + HUD visível**: não conferi que
  inimigos/timers congelam (o `WorldHealthBar` usa `Time.GetTicksMsec`, que ignora
  a pausa) nem que uma sequência de 0,9 s vence onde 0,5 s falha -- só que o
  buffer nasce com a janela escolhida.
