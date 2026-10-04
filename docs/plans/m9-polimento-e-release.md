# M9 — Polimento e release

**Objetivo:** balanceamento final, performance dentro do orçamento e
executáveis para Windows, Linux e macOS. **Build 0.1.**

**Esforço:** 6–10 dias · **Depende de:** M8

## Entregáveis

- Balanceamento validado por playtest
- 40 inimigos ≥ 60 fps na máquina de referência
- Zero crash conhecido
- Builds dos 3 sistemas operacionais
- `THIRD-PARTY-NOTICES.md` e `LICENSE` no pacote
- Tag `v0.1.0`

## Tarefas

### 1. Balanceamento

- [ ] 10 partidas completas com cada personagem, registradas em
      `docs/playtests/`
- [ ] Ajustar HP dos inimigos até que as ondas tenham a curva alvo
      (spec 10 — onda 1 confortável, 3 tensa, 4 quase-morte, 5 exigindo forma)
- [ ] Ajustar custo e dreno de mana até que transformar seja uma decisão
- [ ] Ajustar cooldowns até que todas as 4 habilidades sejam usadas
      — **habilidade nunca usada é habilidade mal balanceada**
- [ ] Verificar os dois arquétipos com dificuldade comparável
- [ ] Atualizar as tabelas das specs 04, 08, 09 e 10 com os valores finais

### 2. Performance

- [x] `--bench=40enemies` com p50/p99 de frame time
      (harness repetível e relatório CSV; execução headless de 60 s no ticket 38)
- [ ] Comparar com o orçamento da [spec 15](../specs/15-qualidade-testes-e-performance.md) §3
- [ ] Profiler: render, física, IA, animação, gameplay, UI
- [ ] Eliminar alocação por frame no hot path
- [ ] `AnimationTree` desligado fora do frustum
- [x] Testar em hardware mais fraco que a máquina de referência (GeForce MX110; execução gráfica reprovou 60 fps, resultados no ticket 38)
- [ ] Verificar VRAM ≤ 1.5 GB

**Validação parcial em 2026-10-03:** o benchmark de 40 inimigos foi medido em
modo headless e em uma GPU local GeForce MX110 abaixo da classe de referência.
Na resolução 1080p, a execução gráfica final de 60 s ficou em p50 29,48 ms e
p99 50,03 ms, ainda acima do orçamento de 60 fps. O fade da câmera seleciona
todas as geometrias que cruzam o segmento de oclusão; instâncias de `MultiMesh`
recebem fade por proxy seletivo. A sonda gráfica confirmou o fade e o restauro
de múltiplas instâncias dentro do mesmo colisor. Medir e otimizar na máquina de
referência segue pendente. Os tempos, alocações restantes e validações manuais
estão registrados no
[ticket 38](../../.scratch/contenda-m0-m9/issues/38-desempenho-e-estabilidade.md).
A execução final de 60 s atribuiu 0 bytes gerenciados ao fader em 3.603 ticks;
as alocações do restante do jogo continuam pendentes de análise.

**Decisão do usuário em 2026-10-04:** encerrar o ticket 38 para priorizar as
builds do ticket 39 e adiar foco adicional em desempenho. O benchmark local
reprovou 60 fps e p99; a medição no hardware de referência, a sessão de 30 min
e os testes manuais seguem sem aprovação. Estes itens ficam como limitações
aceitas da versão 0.1.0, não como critérios tecnicamente aprovados.

### 3. Estabilidade

- [ ] Sessão de 30 minutos sem crash nem vazamento
- [x] Reciclagem de 500 inimigos sem degradação (500 ciclos reais de `Acquire/Release` aprovados)
- [x] 50 transições de cena sem vazar nó (79 transições confirmadas; contagem de nós estável)
- [ ] Alt-tab, mudança de resolução e troca de monitor durante o jogo
- [ ] Todos os casos de borda das configurações
- [ ] Rodar com `settings.cfg` e `profile.cfg` corrompidos

### 4. Game feel final

- [ ] Revisar hitstop, shake e knockback com os assets finais
- [ ] Timing de todas as transições de UI
- [ ] Mixagem de áudio (nada estourando, nada inaudível)
- [ ] Curva de dificuldade do primeiro minuto — é onde se perde o jogador

### 5. Acessibilidade

- [ ] Screen shake reduzível a 0
- [ ] Guia de combos desativável
- [ ] Janela do buffer de comandos configurável (curta/normal/longa)
- [ ] Nenhuma informação comunicada só por cor
- [ ] Texto legível a 1080p sem esforço

### 6. Build

- [x] Presets de export para Windows x86_64, Linux x86_64 e macOS universal (`export_presets.cfg`)
- [ ] Ícone, nome e versão `0.1.0` em `project.godot` e no `.csproj`
- [x] Excluir `docs/`, `tests/`, `tools/`, `src/Tools/` e `scenes/debug/` dos recursos exportados
- [x] Export release, sem debug (sem wrapper de console nem símbolos; atalhos e logs de diagnóstico condicionados a build de debug)
- [ ] Testar cada build numa máquina **sem SDK instalado**
- [ ] Empacotar com `LICENSE`, `THIRD-PARTY-NOTICES.md`, `GODOT_COPYRIGHT.txt` e `README.txt` (`LICENSE` ainda aguarda decisão do usuário)

### 7. CI de release

- [x] Job disparado por tag `v*` exportando os 3 presets e validando a versão da tag
- [x] Upload dos três ZIPs como artifacts da execução de release
- [x] Godot headless na CI agora **bloqueante** (o job tolerante do M0 vira
      obrigatório aqui)

### 8. Documentação

- [ ] `README.md` com screenshots e instruções
- [x] `CHANGELOG.md` da 0.1.0 com escopo e limitações conhecidas, incluindo ausência de versão Web
- [ ] Specs refletindo o que foi realmente construído
- [ ] `docs/plans/backlog-pos-mvp.md` priorizado com o que se aprendeu

### 9. Release

- [ ] Tag `v0.1.0`
- [ ] Release no GitHub com os 3 zips
- [ ] Notas de versão com escopo e limitações conhecidas
      (**incluindo: sem versão Web**)

## Critérios de aceite

- [ ] Uma partida completa (5 ondas) é vencível e desafiadora com ambos
- [ ] 40 inimigos ≥ 60 fps na máquina de referência
- [ ] Sessão de 30 min sem crash
- [ ] As 3 builds rodam em máquinas limpas
- [ ] Todas as 4 habilidades de cada personagem são úteis
- [ ] `THIRD-PARTY-NOTICES.md` completo e correto
- [ ] Um jogador novo chega à onda 3 na segunda tentativa

## Checklist de release

- [ ] Versão consistente em `project.godot`, `.csproj` e na tag
- [ ] Nenhum asset de licença incompatível
- [ ] Nenhum log de debug em release
- [ ] Flags de debug desativadas
- [ ] Configurações persistindo nos 3 SOs
- [ ] `CHANGELOG` atualizado
- [ ] Build baixada de fora do repositório e testada do zero

## Riscos

| Risco | Mitigação |
|---|---|
| Balanceamento infinito | timebox de 3 dias; publicar 0.1 e iterar com feedback real |
| Performance só resolvida no fim | orçamento medido desde o M5, não aqui |
| macOS exigindo notarização | fora do escopo da 0.1; distribuir com instrução de contorno |
| "Só mais uma feature" | o escopo do MVP está congelado desde a visão geral; novidade vai para o backlog |

## Depois da 0.1

Ver [backlog pós-MVP](backlog-pos-mvp.md). Prioridade nº 1: **feedback de
jogadores reais**. A ordem do backlog deve ser reescrita depois de assistir dez
pessoas jogando — não antes.
