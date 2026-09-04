# M7 — UI, menus e settings

**Objetivo:** fechar o ciclo. Menu → Modo → Personagem → Arena → Resultado →
Menu, com pause e configurações persistidas.

**Esforço:** 6–8 dias · **Depende de:** M6

**Ao fim deste milestone o MVP 0.1 está fechado** — o jogo é jogável do início
ao fim por alguém que nunca viu o projeto.

Specs: [11 — UI e menus](../specs/11-ui-hud-e-menus.md) ·
[14 — Configurações](../specs/14-configuracoes-persistencia-e-build.md)

## Entregáveis

- `SceneRouter` com carregamento assíncrono e tela de loading
- Menu principal, seleção de modo, seleção de personagem
- Menu de configurações com 4 abas, incluindo rebind
- Menu de pause
- HUD final estilizado
- Persistência em `user://settings.cfg` e `user://profile.cfg`

## Tarefas

### 1. `SceneRouter`

- [ ] `GoTo(path, showLoading)` com `ResourceLoader.LoadThreadedRequest`
- [ ] `LoadingScreen.tscn` com barra de progresso real
- [ ] Fade out/in de 0.25 s
- [ ] **Despausa e limpa `GetTree().Paused` em toda transição**
- [ ] Evento `SceneChanged`

### 2. Tema

- [ ] `assets/ui/theme/contenda.tres` — único `Theme` do projeto
- [ ] Uma família de fonte, 3 pesos; tamanhos ≥ 16 px em 1080p
- [ ] Paleta: jogador ciano · inimigo vermelho · elite dourado
- [ ] Contraste ≥ 4.5:1 para texto do HUD sobre a arena
- [ ] Zero estilo inline em nó — tudo pelo tema

### 3. Menu principal

- [ ] `MainMenu.tscn` — INICIAR · CONFIGURAÇÕES · SAIR
- [ ] Fundo: a arena em 3D com a câmera de combate em órbita lenta
- [ ] Navegação por teclado e mouse
- [ ] Confirmação ao sair

### 4. Seleção de modo

- [ ] `GameModeMenu.tscn` com cards
- [ ] HORDE jogável; 1 VS 1, BOSS RUSH e ONLINE visíveis com badge "EM BREVE"
- [ ] Descrição de uma linha por modo
- [ ] Voltar com Esc

### 5. Seleção de personagem

- [ ] `CharacterSelectMenu.tscn` com dois cards
- [ ] **Preview 3D real** por `SubViewport` com o modelo em idle
- [ ] Barras de rating vindas de `CharacterDefinition.Rating*`
- [ ] Lista de habilidades e transformação geradas dos `.tres`
- [ ] Confirmar grava em `GameSession.SelectedCharacter` e vai para a arena
- [ ] A tela **não** conhece os personagens — lê `data/characters/roster.tres`

### 6. Configurações

- [ ] `SettingsMenu.tscn` com abas Vídeo · Áudio · Controles · Jogo
- [ ] `GameSettings`, `SettingsStore` (`ConfigFile` em `user://settings.cfg`)
- [ ] `Apply` idempotente, chamado no boot pelo `GameBootstrap`
- [ ] `Version` com fallback para padrões em versão desconhecida
- [ ] **Vídeo:** modo de janela, resolução, VSync, limite de FPS, sombras, SSAO,
      AA, escala de renderização
- [ ] **Áudio:** 5 buses com slider em dB
- [ ] **Controles:** rebind com detecção de conflito e "restaurar padrões"
- [ ] **Jogo:** screen shake, números de dano, guia de combos, **janela do
      buffer de comandos**, idioma
- [ ] Usável do menu principal **e** do pause (mesma cena, como overlay)

### 7. Pause

- [ ] `PauseMenu.tscn` — Continuar · Configurações · Reiniciar · Menu
- [ ] `GetTree().Paused` com blur no fundo
- [ ] HUD visível atrás do blur (o jogador pausa para ler as combinações)
- [ ] `ProcessMode.Always` no menu; todo o resto pausa

### 8. HUD final

- [ ] Aplicar o tema a `HealthBar`, `ManaBar`, `AbilityGuide`,
      `TransformationSelector`, `WaveBanner`
- [ ] Camada de dano atrasada na barra de vida
- [ ] Guia de combos com os 6 estados de linha completos
- [ ] Cor da barra de mana acompanhando a forma ativa
- [ ] Layout responsivo entre 16:9 e 21:9, margem de segurança de 3%
- [ ] Toggle de guia de combos e de números de dano lendo as configurações

### 9. Perfil

- [ ] `user://profile.cfg` com totais e melhor resultado por personagem
- [ ] Escrita atômica (`.tmp` + rename)
- [ ] Recorde exibido na tela de resultados e na seleção de personagem

### 10. Localização

- [ ] `pt-BR` e `en-US` em CSV de tradução
- [ ] Nenhuma string de UI hardcoded em `.cs` ou `.tscn`
- [ ] Troca de idioma em runtime sem reiniciar

## Critérios de aceite

- [ ] O ciclo Menu → Modo → Personagem → Arena → Resultado → Menu fecha
- [ ] Configuração alterada sobrevive a fechar e reabrir o jogo
- [ ] Rebind de `command_confirm` funciona e o HUD mostra a nova tecla
- [ ] Apagar `settings.cfg` abre com padrões, sem crash
- [ ] Sair do pause para o menu **não** deixa o menu pausado
- [ ] O jogo é jogável só com teclado + mouse, sem tutorial externo
- [ ] Nenhum widget usa `GetNode` para achar o jogador
- [ ] Trocar de personagem atualiza toda a guia de combos automaticamente
- [ ] Sem vazamento de nó ao navegar 20× entre as telas

## Teste de usuário — o critério real

Alguém que nunca viu o projeto deve conseguir, **sem ajuda**:

1. abrir o jogo e iniciar uma partida;
2. entender que WASD move e M1 ataca;
3. **executar uma habilidade de propósito em até 2 minutos**;
4. descobrir a transformação sozinho;
5. chegar pelo menos à onda 2.

Se o passo 3 falhar, o problema é a guia de combos — não o jogador.

## Riscos

| Risco | Mitigação |
|---|---|
| UI consumindo mais tempo que o previsto | tema único e widgets pequenos; nada de UI "bonita" antes de funcional |
| Menu nascendo pausado | despausa centralizada no `SceneRouter` |
| Rebind quebrando o jogo | validação de conflito + "restaurar padrões" sempre acessível |
| HUD ilegível sobre a arena | contraste ≥ 4.5:1 e fundo semi-opaco atrás da guia de combos |

## Go/no-go — fim do MVP

**Pergunta:** uma partida completa prende por 10 minutos?

Se não, ajustar ritmo de ondas e ganho de mana **antes** do M8. Arte não
conserta ritmo ruim, só o encarece.
