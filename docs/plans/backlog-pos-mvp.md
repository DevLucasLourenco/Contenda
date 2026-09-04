# Backlog pós-MVP

Tudo que **não** entra na 0.1. Existe para que ideias novas tenham um lugar sem
contaminar o escopo em andamento (risco R3).

> **A ordem abaixo é provisória.** Deve ser reescrita depois de assistir dez
> pessoas jogando a 0.1. Prioridade de backlog antes de feedback real é chute.

## Prioridade 1 — o que o MVP prova que falta

| Item | Por quê | Esforço |
|---|---|---|
| **Horde infinito com escalonamento** | `WaveSetDefinition.LoopWithScaling` já existe; multiplica a vida útil da 0.1 | P |
| **Mais transformações por personagem** | o seletor já mostra `???`; o sistema aguenta N formas | P |
| **Mais habilidades (6–8 por personagem)** | só criar `.tres`; sequências de 3 tokens ainda não exploradas | P |
| **Segunda arena** | uma só arena cansa em 30 min | M |
| **Dodge/roll no Space** | ação já reservada no `InputMap`; melhora muito o combate | M |
| **Sistema de buffs/debuffs com duração** | `StatBlock` já suporta; abre veneno, lentidão, escudo | M |

## Prioridade 2 — modos de jogo

Todos entram como uma classe `IGameMode`, sem tocar no núcleo.

| Modo | Notas | Esforço |
|---|---|---|
| **Boss Rush** | reusa `warlord` + 2 bosses novos | M |
| **Survival** | horda infinita com objetivo de tempo | P |
| **1 vs 1** | exige IA de duelo (comportamento novo, não só parâmetros) | G |
| **Tournament** | encadeia partidas 1v1 | M |
| **Co-op Horde** | exige rede — ver prioridade 4 | GG |

## Prioridade 3 — conteúdo

| Item | Notas | Esforço |
|---|---|---|
| Personagem 3 (mago / à distância mágico) | valida de verdade a arquitetura data-driven | M |
| Personagem 4 e 5 | idem | M cada |
| 5+ tipos de inimigo | atirador de área, suicida, invocador | M |
| Elites com modificadores | veloz, blindado, explosivo — aleatórios por onda | M |
| Modelos comprados para o Gunslinger | *Female Gunner 001/002*, US$ 22,50–30 | P |
| Sistema de itens/pickups | vida, mana, buffs temporários durante a onda | M |

## Prioridade 4 — sistemas grandes

| Item | Notas | Esforço |
|---|---|---|
| **Progressão entre partidas** | desbloquear habilidades/formas com pontos | G |
| **Multiplayer online** | Godot high-level multiplayer; reprojeto de input e estado | GG |
| **Editor de arena** | ferramenta interna para variar cenários rápido | G |
| **Replay/recorde** | grava `IntentFrame` por tick — barato com o desenho atual | M |
| **Modding via `.tres`** | conteúdo já é data-driven; falta um loader de pasta externa | M |

## Prioridade 5 — polimento e plataforma

| Item | Notas |
|---|---|
| Suporte a gamepad completo | mapeamento já desenhado na spec 03 §10 |
| Mais idiomas | infra de localização pronta no M7 |
| Steam (conquistas, nuvem) | exige integração com a SDK |
| Trilha original | substituir CC0 |
| Câmera ortográfica opcional | `CameraSettings.UseOrthogonal` já previsto |
| Modo foto | fácil com câmera desacoplada do player |
| Dificuldades Difícil / Pesadelo | multiplicadores por `WaveSetDefinition` |

## Explicitamente fora

| Item | Motivo |
|---|---|
| **Exportação Web** | Godot 4 + C# não suporta; mudaria a stack inteira (ADR-001) |
| Mobile | os controles são desenhados para teclado e mouse |
| VR | incompatível com câmera fixa |
| Mundo aberto | o jogo é de arena |
| Crafting / economia | não pertence ao gênero escolhido |

## Regra de entrada

Toda ideia nova durante o MVP vira **uma linha aqui**, com uma frase de
justificativa — e a conversa acaba. Nenhuma exceção, mesmo boa.

Legenda: P = dias · M = 1–2 semanas · G = 3–6 semanas · GG = meses
