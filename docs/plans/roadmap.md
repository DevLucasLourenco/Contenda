# Roadmap

Dez milestones do repositório vazio à build 0.1. Cada um entrega **algo
jogável ou verificável** — nada de "só refatorar" ou "só arquitetura".

## Visão geral

```
M0  Fundação técnica          ─┐
M1  Movimento e câmera 2.5D    │  fundação — prova que é 3D de verdade
M2  Atributos e combate básico │
M3  Comandos e habilidades     │  a mecânica assinatura
M4  Transformações             ├─  MVP 0.1
M5  Inimigos e IA              │
M6  Horde Mode                 │  o jogo
M7  UI, menus e settings      ─┘
────────────────────────────────
M8  Arte, animação e áudio        polimento
M9  Polimento e release           build 0.1
```

## Tabela

| # | Milestone | Entrega verificável | Esforço |
|---|---|---|---|
| **M0** | [Fundação](m0-fundacao.md) | projeto Godot .NET compila, CI verde, convenções escritas | 2–3 d |
| **M1** | [Movimento e câmera](m1-movimento-e-camera.md) | cápsula anda na arena, câmera fixa não gira | 4–5 d |
| **M2** | [Atributos e combate básico](m2-atributos-e-combate-basico.md) | vida, mana, M1 de espada e revólver batendo em manequim | 5–7 d |
| **M3** | [Comandos e habilidades](m3-comandos-e-habilidades.md) | `W W`+M2 executa Dash Slash; HUD mostra o buffer ao vivo | 6–8 d |
| **M4** | [Transformações](m4-transformacoes.md) | scroll seleciona, M3 ativa, mana drena, reverte em 0 | 3–4 d |
| **M5** | [Inimigos e IA](m5-inimigos-e-ia.md) | 40 inimigos navegam, atacam, morrem e reciclam | 7–9 d |
| **M6** | [Horde Mode](m6-horde-mode.md) | 5 ondas do início ao fim, vitória e derrota | 5–6 d |
| **M7** | [UI, menus e settings](m7-ui-menus-e-settings.md) | ciclo completo de telas — **MVP 0.1 fechado** | 6–8 d |
| **M8** | [Arte, animação e áudio](m8-arte-animacao-e-audio.md) | modelos reais animados, VFX, SFX | 8–12 d |
| **M9** | [Polimento e release](m9-polimento-e-release.md) | balanceamento, 60 fps, executáveis dos 3 SOs | 6–10 d |

**Total estimado:** 52–72 dias de trabalho focado.

> Estimativas são para **uma pessoa trabalhando com foco**, incluindo o
> aprendizado da API do Godot em C#. Um dev experiente em Godot corta ~30%.
> As faixas são deliberadamente largas: M5 e M8 são os que mais escorregam.

## Ordem de dependências

```
M0 ──▶ M1 ──▶ M2 ──┬──▶ M3 ──▶ M4 ──┐
                   │                 ├──▶ M6 ──▶ M7 ──▶ M8 ──▶ M9
                   └──▶ M5 ──────────┘
```

M3/M4 e M5 são **paralelizáveis** se houver duas pessoas: um trilho de
"personagem do jogador", outro de "inimigos e IA". Ambos dependem apenas dos
componentes do M2.

## Por que esta ordem

1. **M1 antes de tudo o mais visível.** O erro histórico do projeto foi derivar
   para 2D. Travar a câmera e a estrutura 3D no primeiro milestone jogável
   torna essa deriva impossível depois.
2. **M2 antes de M3.** Habilidades sem vida, mana e dano não são testáveis.
3. **M3 antes de M5.** A mecânica assinatura precisa ser validada num manequim
   antes de existir horda; se o sistema de comandos não for divertido contra um
   alvo parado, não vai ser contra 40.
4. **M5 depois de M2, não de M4.** Inimigos só precisam de vida, movimento e
   combate — não de transformações.
5. **M8 (arte) por último entre os de conteúdo.** Cápsulas e primitivas até o
   M7. Investir em arte antes de o gameplay fechar é a forma mais cara de
   descobrir que a mecânica não funciona.

## Marcos de decisão (go/no-go)

| Após | Pergunta | Se a resposta for "não" |
|---|---|---|
| M1 | A câmera 2.5D é legível e agradável? | ajustar pitch/FOV/distância antes de seguir; testar ortográfica |
| M3 | Executar habilidades por sequência é **divertido**? | é o pivô mais barato agora: reduzir para 2 tokens, ou ir para teclas 1–4 com combos opcionais |
| M5 | 40 inimigos rodam a 60 fps? | reduzir o teto para 25 e ajustar o design das ondas |
| M7 | Uma partida completa prende por 10 minutos? | mexer em ritmo de ondas e ganho de mana antes de investir em arte |

O marco após o M3 é o mais importante do projeto. É onde se descobre se a ideia
central funciona, com custo ainda baixo para mudar.

## Higiene contínua (todos os milestones)

- Um branch por milestone; PRs pequenos dentro dele.
- Portões de qualidade da [spec 15](../specs/15-qualidade-testes-e-performance.md)
  valem desde o M0.
- Spec atualizada no mesmo PR que muda o comportamento. Documentação que mente é
  pior que documentação ausente.
- Playtest semanal a partir do M5, registrado em `docs/playtests/`.
