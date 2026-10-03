# M8 — Arte, animação e áudio

**Objetivo:** trocar cápsulas e primitivas por modelos reais animados, com VFX
e áudio. O gameplay já está fechado — aqui ele ganha corpo.

**Esforço:** 8–12 dias · **Depende de:** M7

Spec: [13 — Assets, animação e licenças](../specs/13-assets-animacao-e-licencas.md)

O ticket 34 entregou modelos, armas procedurais e animações dos dois
personagens jogáveis. O ticket 35 acrescentou os cinco modelos KayKit de
inimigos, animações de alerta/ataque/dano/morte e módulos Kenney para a arena.
VFX e áudio continuam pendentes; a confirmação visual da legibilidade e a
medição final de desempenho também aguardam validação em máquina de referência.

> Deliberadamente **depois** do MVP. Investir em arte antes de o gameplay
> fechar é a forma mais cara de descobrir que a mecânica não funciona.

## Entregáveis

- Modelos CC0 dos dois personagens e dos 5 inimigos; visuais procedurais para as
  armas
- Animações KayKit no rig compartilhado, ligadas por `AnimationSet`; `BoneMap`
  para rigs futuros de outra fonte
- `AnimationTree` com locomoção, ataque, habilidade, hit, dash e morte
- VFX de golpe, impacto, habilidade, transformação, spawn e morte
- SFX completo e trilha
- `THIRD-PARTY-NOTICES.md` preenchido

## Tarefas

### 1. Aquisição

- [x] Baixar **KayKit Adventurers** (CC0) → modelos Knight e Rogue dos personagens jogáveis
- [x] Baixar **KayKit Character Animations** (CC0) → bancos de animação para os dois rigs
- [ ] Baixar **Quaternius Ultimate Animated Character Pack** (CC0) →
      inimigos
- [ ] Baixar **Quaternius Animated Guns Pack** (CC0) → revólver
- [x] Registrar `SOURCE.md` dos modelos jogáveis e dos bancos de animação
- [ ] Verificar que **nada** CC-BY-NC entrou (auditoria por grep)

> A alternativa paga (*Female Gunner 001/002*, US$ 22,50–30) corresponde melhor
> à descrição da personagem. Reavaliar **aqui**, com o jogo já jogável: se o
> modelo CC0 não vender a fantasia, é o melhor momento para comprar. Trocar
> depois é uma linha no `.tres`.

### 2. Preparação no Blender

- [ ] Converter FBX → `.glb` quando necessário
- [ ] Escala: personagem ≈ 1.8 m
- [ ] Frente no eixo `-Z`, origem entre os pés
- [ ] Limpar animações não usadas
- [ ] Verificar orçamento: ≤ 15k tri (jogável), ≤ 6k (inimigo), ≤ 70 ossos

### 3. Import no Godot

- [x] Configuração de import da spec 13 §4.3 para os assets KayKit
- [x] `Generate LODs` e `Create Shadow Meshes` ligados
- [x] Materiais GLB mantidos embutidos (sem extração para `.tres`)
- [x] Commitar os `.import`

### 4. Retarget

- [x] Reusar o rig KayKit compartilhado de 23 ossos para Knight, Rogue e clipes
- [x] Copiar os clipes selecionados para `AnimationLibrary` por personagem
- [x] Remover trilhas de posição do osso `root`; deslocamento continua no código
- [ ] Configurar `BoneMap` para rigs futuros de outra fonte

### 5. `AnimationSet`

- [x] `animset_swordsman` e `animset_gunslinger` com `AbilityAnimations` por `abilityId`
- [x] Inimigos KayKit usam os AnimationSets de combate compatíveis com o rig compartilhado; alerta é acionado pelo estado da IA
- [ ] `animset_berserker`, `animset_overdrive` e outros arquétipos futuros (spec 12 §4)
- [x] Nenhum nome de clipe de asset hardcoded em `.cs`

### 6. `AnimationTree`

- [x] `AnimationNodeBlendTree` com `BlendSpace1D` de locomoção (Idle ↔ Walk ↔ Run)
- [x] OneShots para alerta, ataques, habilidades, recarga, mobilidade, hit, transformação e morte
- [x] `Death` terminal
- [x] `xfade` de 0.1 s
- [x] **Root motion desligado** — deslocamento é do `MovementComponent`
- [x] `CharacterAnimator` traduzindo eventos do gameplay em parâmetros do tree

### 7. Sincronização de combate

- [x] Ajustar janelas dos combos de espada aos quadros de impacto dos clipes KayKit
- [ ] Ajustar `CastTime` das habilidades ao wind-up das animações
- [ ] Ajustar windups dos inimigos
- [ ] **Não** mover as janelas para tracks de animação (spec 13 §6)

### 8. VFX

- [x] Trail mesh de espada, clarão de boca e tracer em pools reutilizados
- [x] Impacto por tipo de dano; crítico tem forma, cor e som exclusivos
- [x] VFX por habilidade (8 estilos e cores configurados no catálogo)
- [x] Ativação e aura persistente de cada forma
- [x] Efeito de materialização de spawn e impacto visual na morte
- [x] Decal circular de área para ataques telegrafados

### 9. Áudio

- [x] Buses Master → Music, SFX, UI, Ambience (aplicados por `SettingsStore`)
- [x] `AudioStreamPlayer3D` pooled; limite de 3 instâncias por arquivo, pitch ±8%
- [x] SFX: passos, golpes, impactos, tiros, recarga, habilidades, transformação,
      dano recebido, morte, aviso de ataque e UI
- [x] Música de menu, combate e chefe com crossfade
- [x] Ambiência contínua da cidade
- [x] Fontes CC0 com `SOURCE.md` por pasta e registro em `THIRD-PARTY-NOTICES.md`

### 10. Cenário

- [x] Substituir as primitivas visuais de ruas, calçadas, fachadas e obstáculos por módulos CC0; a praça e as colisões mantêm a geometria de jogo
- [ ] Iluminação final, `WorldEnvironment` com SSAO, tonemap e bloom
- [x] **Rebake da navmesh** após incluir as colisões dos carros
- [ ] Verificar que a legibilidade sob a câmera fixa não piorou

### 11. Notices

- [x] `THIRD-PARTY-NOTICES.md` completo para os assets incorporados, no formato da spec 13 §9

## Critérios de aceite

- [x] Os dois personagens jogáveis usam modelos reais com animações no rig KayKit compartilhado
- [x] Os 5 inimigos têm modelo, animações de locomoção/alerta/ataque/dano e morte no rig KayKit compartilhado
- [x] Armas presas corretamente às mãos via `BoneAttachment3D`
- [x] Golpes conectam **visualmente** no momento do dano
- [x] Transformação é visualmente inconfundível
- [x] Todo asset incorporado tem `SOURCE.md` e licença compatível
- [x] Auditoria dos assets não encontrou CC-BY-NC no repositório
- [ ] Trocar o `.glb` de um personagem do mesmo rig exige só novo
      `AnimationSet`; rigs diferentes também exigem `BoneMap`
- [ ] Performance mantida: 40 inimigos ≥ 60 fps **com modelos reais**

## Riscos

| Risco | Mitigação |
|---|---|
| **Retarget deformando o rig** | validar um personagem completo antes de processar todos |
| Estilos visuais inconsistentes entre packs | KayKit e Quaternius são ambos low-poly estilizados — combinam bem; unificar por material e iluminação |
| Performance caindo com modelos reais | LODs no import, orçamento de triângulos, medir de novo com 40 inimigos |
| Animações não batendo com as janelas de hit | ajustar `MeleeComboStep`, nunca o contrário |
| Milestone escorregando | é o segundo mais arriscado do projeto; cortar VFX secundário antes de cortar animação |
