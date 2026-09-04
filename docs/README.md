# Documentação — Contenda

Toda a documentação está dividida em **specs** (o que o jogo é e como cada
sistema se comporta) e **plans** (em que ordem construímos, com critérios de
aceite por etapa).

## Ordem de leitura sugerida

1. [00 — Visão geral, escopo e glossário](00-visao-geral.md)
2. [Specs 01–15](#specs) — leia 01, 02 e 03 antes de escrever qualquer código
3. [Roadmap](plans/roadmap.md) e o milestone corrente

## Specs

| # | Documento | Cobre |
|---|---|---|
| 01 | [Arquitetura técnica](specs/01-arquitetura-tecnica.md) | stack, solution, componentes, eventos, ciclo de vida |
| 02 | [Câmera e mundo 2.5D](specs/02-camera-e-mundo-25d.md) | rig, ângulos, follow, arena, navmesh, camadas de colisão |
| 03 | [Input, comandos e combos](specs/03-input-comandos-e-combos.md) | InputMap, CommandBuffer, AbilityComboResolver |
| 04 | [Atributos: vida, mana e stats](specs/04-atributos-vida-mana-stats.md) | HealthComponent, ManaComponent, StatBlock, modificadores |
| 05 | [Habilidades](specs/05-habilidades.md) | AbilityDefinition, execução, cooldown, custos |
| 06 | [Transformações](specs/06-transformacoes.md) | seleção por scroll, ativação, drain, reversão |
| 07 | [Combate, armas e dano](specs/07-combate-armas-e-dano.md) | IWeapon, combos de M1, hitscan, DamageInfo, i-frames |
| 08 | [Personagens](specs/08-personagens.md) | Swordsman, Gunslinger, tabelas de balanceamento |
| 09 | [Inimigos e IA](specs/09-inimigos-e-ia.md) | EnemyBrain FSM, navegação, pooling |
| 10 | [Modos de jogo e Horde](specs/10-modos-de-jogo-horde.md) | IGameMode, WaveDirector, SpawnDirector |
| 11 | [UI, HUD e menus](specs/11-ui-hud-e-menus.md) | HUD de combos, seletor de transformação, fluxo de telas |
| 12 | [Dados, Resources e conteúdo](specs/12-dados-resources-e-conteudo.md) | catálogo de `.tres`, convenções de autoria |
| 13 | [Assets 3D, animação e licenças](specs/13-assets-animacao-e-licencas.md) | Quaternius, KayKit, Mixamo, pipeline de import/retarget |
| 14 | [Configurações, persistência e build](specs/14-configuracoes-persistencia-e-build.md) | settings, saves, export presets |
| 15 | [Qualidade, testes e performance](specs/15-qualidade-testes-e-performance.md) | camadas testáveis, orçamentos de frame |

## Plans

| Milestone | Documento | Entrega |
|---|---|---|
| M0 | [Fundação](plans/m0-fundacao.md) | projeto Godot .NET compilando, CI, convenções |
| M1 | [Movimento e câmera](plans/m1-movimento-e-camera.md) | cápsula andando numa arena com a câmera 2.5D |
| M2 | [Atributos e combate básico](plans/m2-atributos-e-combate-basico.md) | vida, mana, M1 de espada e revólver |
| M3 | [Comandos e habilidades](plans/m3-comandos-e-habilidades.md) | WASD+M2 executando habilidades data-driven |
| M4 | [Transformações](plans/m4-transformacoes.md) | scroll seleciona, M3 ativa, mana drena |
| M5 | [Inimigos e IA](plans/m5-inimigos-e-ia.md) | inimigos navegando, atacando e morrendo |
| M6 | [Horde Mode](plans/m6-horde-mode.md) | ondas, spawn, pool, derrota e resultados |
| M7 | [UI, menus e settings](plans/m7-ui-menus-e-settings.md) | fluxo completo de telas — **fecha o MVP 0.1** |
| M8 | [Arte, animação e áudio](plans/m8-arte-animacao-e-audio.md) | modelos reais, AnimationTree, VFX, SFX |
| M9 | [Polimento e release](plans/m9-polimento-e-release.md) | balanceamento, performance, build 0.1 |

Complementos: [convenções de código](plans/convencoes-de-codigo.md) ·
[riscos e ADRs](plans/riscos-e-decisoes.md) ·
[backlog pós-MVP](plans/backlog-pos-mvp.md)
