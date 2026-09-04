# 00 — Visão geral, escopo e glossário

## 1. O que estamos construindo

Um jogo de **ação/combate 2.5D** para PC (Windows/Linux/macOS), single-player no
MVP, em que o jogador controla um de dois personagens e sobrevive a ondas de
inimigos numa arena 3D.

A definição operacional de **2.5D** neste projeto:

> Simulação, física, colisão, navegação e renderização são **3D**. O jogador se
> move no plano **X/Z**; **Y** é altura real (pulos, desníveis, projéteis em arco).
> A `Camera3D` fica **travada** num ângulo superior inclinado, segue a posição do
> alvo e **nunca gira** com o personagem.

Referências visuais: *V Rising*, *Battlerite*, *Darksiders Genesis*.
**Não** é *Dragon Ball FighterZ* — não há plano de luta 2D nem sprites.

## 2. Pilares de design

| Pilar | Consequência prática |
|---|---|
| Profundidade real | câmera **perspectiva** com FOV baixo, não ortográfica |
| Leitura clara da arena | ângulo fixo, sem rotação, sem colisão de câmera com parede |
| Expressão por comandos | habilidades saem de **sequências de WASD + M2**, não de teclas 1–4 |
| Risco/recompensa por mana | transformações **drenam mana continuamente** |
| Conteúdo sem código | novo personagem/habilidade/onda = novo `.tres` |

## 3. Pilares de arquitetura

1. **Tudo 3D.** Proibido no gameplay: `Node2D`, `CharacterBody2D`, `Camera2D`,
   `Sprite2D`, `Area2D`. (UI usa `Control`, que é 2D por natureza — permitido.)
2. **Composição sobre herança.** `CharacterController` é um contêiner; a lógica
   vive em componentes independentes (`HealthComponent`, `ManaComponent`, …).
   Jogador, inimigo e boss usam **os mesmos** componentes.
3. **Data-driven.** Regras que variam por conteúdo vivem em `Resource` C#
   exportados como `.tres`.
4. **Sem GDScript.** 100% C# — inclusive tooling e editor scripts.
5. **Núcleo testável fora da engine.** Regras puras (dano, cooldown, resolução de
   combo, progressão de ondas) em classes POCO, testáveis sem abrir o Godot.

## 4. Escopo do MVP 0.1

Fecha ao final do [M7](plans/m7-ui-menus-e-settings.md).

**Dentro:**

- Menu principal: Iniciar · Configurações · Sair
- Seleção de modo (só **Horde** jogável; demais como "Em breve")
- Seleção de personagem: **Swordsman** (espada) e **Gunslinger** (revólver)
- 1 arena 3D com navmesh
- Câmera 2.5D fixa
- WASD, M1 (ataque básico), M2 (confirmar comando), scroll (selecionar
  transformação), M3 (ativar), Esc (pause)
- Vida e mana funcionais com regeneração de mana
- **3–4 habilidades por personagem** por sequência de comando
- **1 transformação por personagem**
- HUD com barras, guia de combos com feedback ao vivo e seletor de transformação
- Horde com ondas escaladas, **5–10 inimigos simultâneos**, tela de resultado
- Configurações persistidas (vídeo, áudio, sensibilidade, rebind)

**Fora do MVP (backlog):** multiplayer/online, 1v1, Boss Rush, progressão entre
partidas, loja, mais de 2 personagens, mais de 1 transformação por personagem,
mais de 1 arena, dublagem, exportação Web.

## 5. Restrições conhecidas

| Restrição | Impacto | Mitigação |
|---|---|---|
| Godot 4 + C# **não exporta para Web** | sem demo em navegador | desktop-only no MVP; se Web virar requisito, é uma decisão de reescrita, não de ajuste |
| C# exige o editor **.NET** do Godot | todo dev precisa do SDK .NET | documentado no [M0](plans/m0-fundacao.md) |
| Assets de terceiros têm licenças distintas | risco jurídico | só **CC0 / MIT / Mixamo** entram no repo — ver [spec 13](specs/13-assets-animacao-e-licencas.md) |

## 6. Como usamos as referências pesquisadas

Nenhum projeto de terceiro vira dependência. Usamos **o raciocínio**, e
reescrevemos em C# dentro de `Contenda`.

| Referência | Licença | O que aproveitamos | O que **não** aproveitamos |
|---|---|---|---|
| Isometric 3D Toolkit (Godot C#) | CC BY 4.0 | matemática de câmera isométrica/ortográfica, enquadramento | a API — o autor avisa que ela muda muito |
| Godot Top-Down Template | mista (GDScript+C#) | composição, `HealthComponent`, `BaseWeapon`, data resources, rebind | o jogo em si é 2D e mistura linguagens |
| GDAbilitySystem | MIT | filosofia Ability/Attribute/Effect/Tag do GAS da Unreal | o plugin em si — começamos com versão simples e própria |
| Godot4 3D Characters | MIT | fluxo de IA `Idle→Alert→Follow→Attack→Death`, AnimationTree, character select | o código como base |
| RoboBlast (GDQuest) | código MIT, **arte CC-BY-NC-SA** | melee, tiro, CharacterBody3D, projéteis | **a arte** — licença incompatível com uso comercial |

Se algo de CC BY for efetivamente derivado, a atribuição vai em
`THIRD-PARTY-NOTICES.md` — ver [spec 13](specs/13-assets-animacao-e-licencas.md).

## 7. Glossário

| Termo | Significado neste projeto |
|---|---|
| **Componente** | `Node` filho do personagem com uma responsabilidade única |
| **Definition** | `Resource` C# com dados de conteúdo (`AbilityDefinition`, …) |
| **Token** | um registro de tecla direcional no `CommandBuffer` (W/A/S/D) |
| **Sequência** | lista ordenada de tokens que identifica uma habilidade (`[W,W]`) |
| **Confirmação** | M2 — dispara a habilidade que casa com a sequência atual |
| **Forma / Transformação** | estado temporário que aplica modificadores e drena mana |
| **Onda (Wave)** | grupo de inimigos com composição e ritmo definidos por `.tres` |
| **Arquétipo** | personagem jogável (Swordsman, Gunslinger) |
| **Stat** | atributo numérico derivado de base + modificadores |
