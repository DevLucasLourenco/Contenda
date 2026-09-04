# 11 — UI, HUD e menus

## 1. Fluxo de telas

```
                 ┌────────────────┐
                 │  MAIN MENU     │
                 │  INICIAR       │
                 │  CONFIGURAÇÕES │
                 │  SAIR          │
                 └───┬────────────┘
                     │ INICIAR
                     v
             ┌───────────────────┐
             │  ESCOLHA O MODO   │
             │  [ HORDE ]        │  ← único jogável
             │  [ 1 VS 1 ]  ⏳    │
             │  [ BOSS RUSH ] ⏳  │
             │  [ ONLINE ]  ⏳    │
             └───────┬───────────┘
                     v
        ┌────────────────────────────┐
        │   SELEÇÃO DE PERSONAGEM    │
        │  ┌────────┐   ┌────────┐   │
        │  │SWORDSMAN│  │GUNSLINGER│  │
        │  │ Espada │   │ Revólver│   │
        │  └────────┘   └────────┘   │
        │        [ CONFIRMAR ]       │
        └───────────┬────────────────┘
                    v
              ┌──────────┐
              │  ARENA   │◀── Esc ──▶ PAUSE
              └────┬─────┘
                   v
            ┌──────────────┐
            │  RESULTADOS  │
            └──────────────┘
```

Transições via `SceneRouter.GoTo()` — carregamento assíncrono
(`ResourceLoader.LoadThreadedRequest`) com tela de loading, para que a arena não
congele a aplicação ao carregar.

Modos "Em breve" aparecem **desabilitados e visíveis**. Comunicam a ambição do
projeto e custam nada.

## 2. HUD de combate

```
┌────────────────────────────────────────────────────────────────┐
│ HP ██████████████████░░░░  128/140                             │
│ MP ███████████░░░░░░░░░░░   62/100   ⟳ 5/s                     │
│                                                  ONDA 3   ⏱ 2:14│
│                                                  SCORE  4 250   │
│                                                                │
│                          ENEMY ●                               │
│                   ENEMY ●                                      │
│                                   PLAYER ●                     │
│                ENEMY ●                                         │
│                                                                │
│ ┌────────────────────────────────────────────────────────────┐ │
│ │  [W] [W]      + M2   Dash Slash                            │ │
│ │   S   W       + M2   Rising Slash            ⟳ 2.4s        │ │
│ │   A   D       + M2   Spin Slash                            │ │
│ │   W   A   W   + M2   Heavy Lunge             ⟳ 6.1s        │ │
│ ├────────────────────────────────────────────────────────────┤ │
│ │      ◀   Normal  |  [ BERSERKER ]  |  ???   ▶              │ │
│ └────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────┘
```

### 2.1 Guia de combos — o elemento mais importante do HUD

O requisito é explícito: **as combinações precisam estar visíveis**. Mas ele faz
mais do que listar: é o **feedback ao vivo do `CommandBuffer`**.

Estados de cada linha:

| Estado | Aparência |
|---|---|
| ociosa (buffer vazio) | todos os tokens em cinza, nome em branco |
| **candidata** (buffer é prefixo desta sequência) | tokens já digitados destacados `[W]`, restantes em cinza, linha realçada |
| **pronta para confirmar** (match exato) | linha inteira acesa + prompt `+ M2` pulsando |
| **descartada** (buffer não bate) | esmaecida para 30% de opacidade |
| **em cooldown** | ícone ⟳ com o tempo restante, tokens acinzentados |
| **sem mana** | custo de mana em vermelho |

Progressão ao digitar:

```
buffer vazio           digitou W                  digitou W W
─────────────────      ────────────────────       ──────────────────────
 W  W    Dash           [W]  W    Dash             [W] [W]  Dash  + M2 ✦
 S  W    Rising          S   W    Rising  (30%)     ...          (30%)
 A  D    Spin            A   D    Spin    (30%)     ...          (30%)
 W  A  W Lunge          [W]  A  W Lunge             ...          (30%)
```

Isso resolve dois problemas de uma vez: ensina as sequências e mostra o que o
jogo entendeu do input. Sem isso, o sistema de comandos parece quebrado.

Feedback de falha: M2 sem match faz a lista inteira piscar em vermelho por
0.15 s com um som curto.

### 2.2 Barras de vida e mana

- Barra de vida com **camada de dano atrasada** (a fatia perdida esvazia em
  0.4 s depois do golpe) — comunica quanto se levou.
- Barra de mana muda para a `ThemeColor` da forma enquanto transformado, e
  pulsa quando cai abaixo de 15% com forma ativa.
- Números exatos ao lado das barras; sem números, o jogador não aprende os
  custos.

### 2.3 Seletor de transformação

```
      ◀   Normal  |  [ BERSERKER ]  |  ???   ▶
                      4 mana/s
```

- Selecionado com moldura na `ThemeColor`; ativo com borda pulsante.
- Slots futuros aparecem como `???` — o jogo comunica que há mais.
- Ao rolar o scroll, animação lateral de 0.12 s (senão a troca não é percebida).

### 2.4 Barras de vida dos inimigos

Vida do inimigo fica **sobre o inimigo**, não no HUD. Em modo horda, uma barra
central para "o inimigo" não faz sentido.

- `Sprite3D` com `SubViewport` (ou `Control` reposicionado por
  `UnprojectPosition` — decidir no M8 medindo custo com 40 inimigos).
- Aparece só após o primeiro dano; some 3 s depois do último.
- Elites e boss têm barra maior, com nome.
- **Boss** também recebe uma barra dedicada no topo da tela.

### 2.5 Elementos de modo

`WaveBanner` (onda N, anunciado no centro por 1.5 s), contador de onda, tempo,
score e multiplicador de combo. Todos alimentados por eventos do `IGameMode`,
nunca por polling da cena.

## 3. Menus

### 3.1 Main Menu

```
              C O N T E N D A

                 INICIAR
              CONFIGURAÇÕES
                  SAIR
```

Fundo: a própria arena em 3D com a câmera de combate em órbita lenta — mostra o
jogo antes de o jogador entrar nele, e reusa assets que já existem.

### 3.2 Seleção de modo

Cards com nome, descrição de uma linha e badge "EM BREVE" nos bloqueados.
Foco inicial no Horde.

### 3.3 Seleção de personagem

```
┌──────────────────┐    ┌──────────────────┐
│                  │    │                  │
│    SWORDSMAN     │    │    GUNSLINGER    │
│  (preview 3D)    │    │   (preview 3D)   │
│                  │    │                  │
│ Dano       ████░ │    │ Dano       ███░░ │
│ Alcance    ██░░░ │    │ Alcance    █████ │
│ Velocidade ███░░ │    │ Velocidade ████░ │
│ Resistência████░ │    │ Resistência██░░░ │
│                  │    │                  │
│ ▸ Dash Slash     │    │ ▸ Quick Step Shot│
│ ▸ Rising Slash   │    │ ▸ Explosive Shot │
│ ▸ Spin Slash     │    │ ▸ Fan The Hammer │
│ ▸ Heavy Lunge    │    │ ▸ Deadeye        │
│ ★ Berserker      │    │ ★ Overdrive      │
└──────────────────┘    └──────────────────┘
         [ CONFIRMAR ]      [ VOLTAR ]
```

- Preview 3D real (`SubViewport` com o modelo em idle), não render pré-feito.
- As barras vêm de `CharacterDefinition.Rating*`.
- A lista de habilidades é gerada dos `.tres` — adicionar personagem não altera
  esta tela.

### 3.4 Configurações

Abas: **Vídeo · Áudio · Controles · Jogo**. Conteúdo em
[spec 14](14-configuracoes-persistencia-e-build.md).

Acessível do menu principal **e** do pause, com a mesma cena
(`SettingsMenu.tscn` instanciada como overlay).

### 3.5 Pause

`Esc` durante a partida: `GetTree().Paused = true`, blur no fundo.
Continuar · Configurações · Reiniciar · Sair para o menu.

O HUD permanece visível atrás do blur — o jogador costuma pausar exatamente para
ler as combinações.

## 4. Diretrizes visuais

| Item | Regra |
|---|---|
| Resolução base | 1920×1080, `canvas_items` com `expand`, aspecto preservado |
| Fonte | uma família, 3 pesos, tamanhos ≥ 16 px em 1080p |
| Contraste | mínimo 4.5:1 para texto do HUD sobre a arena |
| Cor de time | jogador ciano · inimigo vermelho · elite dourado |
| Daltonismo | nunca comunicar só por cor: forma/ícone/texto sempre acompanham |
| Animação | 0.10–0.20 s; nada acima de 0.3 s no HUD |
| Segurança de tela | margem de 3% |

Todo o HUD usa um único `Theme` (`ui/theme/contenda.tres`). Nenhum estilo
inline em nó.

## 5. Estrutura de UI

```
scenes/ui/
├── hud/
│   ├── Hud.tscn              [HudController]
│   ├── HealthBar.tscn        [HealthBar]
│   ├── ManaBar.tscn          [ManaBar]
│   ├── AbilityGuide.tscn     [AbilityGuide]
│   ├── AbilityGuideRow.tscn
│   ├── TransformationSelector.tscn
│   ├── WaveBanner.tscn
│   └── WorldHealthBar.tscn
├── menus/
│   ├── MainMenu.tscn · GameModeMenu.tscn · CharacterSelectMenu.tscn
│   ├── SettingsMenu.tscn · PauseMenu.tscn · ResultsScreen.tscn
│   └── LoadingScreen.tscn
└── theme/contenda.tres
```

`HudController` se liga ao `CharacterController` do jogador **uma vez** e
distribui as referências aos widgets. Nenhum widget procura o jogador por conta
própria.

## 6. Critérios de aceite (M7)

- [ ] O fluxo Menu → Modo → Personagem → Arena → Resultado → Menu fecha o ciclo.
- [ ] A guia de combos reage ao input em tempo real, com os 6 estados de linha.
- [ ] Cooldown e falta de mana são legíveis sem tentar executar a habilidade.
- [ ] O jogo é jogável apenas com teclado+mouse, sem tutorial externo.
- [ ] Nenhum widget usa `GetNode` para achar o jogador.
- [ ] Trocar de personagem atualiza toda a guia de combos automaticamente.
