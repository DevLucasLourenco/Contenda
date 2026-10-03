# M8 — Arte, animação e áudio

**Objetivo:** trocar cápsulas e primitivas por modelos reais animados, com VFX
e áudio. O gameplay já está fechado — aqui ele ganha corpo.

**Esforço:** 8–12 dias · **Depende de:** M7

Spec: [13 — Assets, animação e licenças](../specs/13-assets-animacao-e-licencas.md)

O ticket 34 entregou modelos, armas procedurais e animações dos dois
personagens jogáveis. O restante deste milestone — modelos dos inimigos, VFX,
áudio e cenário — continua pendente.

> Deliberadamente **depois** do MVP. Investir em arte antes de o gameplay
> fechar é a forma mais cara de descobrir que a mecânica não funciona.

## Entregáveis

- Modelos CC0 dos dois personagens, das duas armas e dos 5 inimigos
- Animações retargetadas via `BoneMap`, ligadas por `AnimationSet`
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

- [ ] Configuração de import da spec 13 §4.3
- [ ] `Generate LODs` e `Create Shadow Meshes` ligados
- [ ] Materiais extraídos para `.tres`
- [ ] Commitar os `.import`

### 4. Retarget

- [x] Reusar o rig KayKit compartilhado de 23 ossos para Knight, Rogue e clipes
- [x] Copiar os clipes selecionados para `AnimationLibrary` por personagem
- [x] Remover trilhas de posição do osso `root`; deslocamento continua no código
- [ ] Configurar `BoneMap` para rigs futuros de outra fonte

### 5. `AnimationSet`

- [x] `animset_swordsman` e `animset_gunslinger` com `AbilityAnimations` por `abilityId`
- [ ] `animset_berserker`, `animset_overdrive`, inimigos e outros futuros (spec 12 §4)
- [x] Nenhum nome de clipe de asset hardcoded em `.cs`

### 6. `AnimationTree`

- [x] `AnimationNodeBlendTree` com `BlendSpace1D` de locomoção (Idle ↔ Walk ↔ Run)
- [x] OneShots para ataques, habilidades, recarga, mobilidade, hit, transformação e morte
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

- [ ] Trail de espada (`GPUParticles3D` ou mesh de trail)
- [ ] Muzzle flash + tracer do revólver
- [ ] Impacto por tipo de dano
- [ ] VFX por habilidade (8)
- [ ] Ativação e aura persistente de cada forma
- [ ] Marcador de spawn e dissolve de morte
- [ ] Decal de área para ataques telegrafados

### 9. Áudio

- [ ] Buses Master → Music, SFX, UI, Ambience
- [ ] `AudioStreamPlayer3D` pooled; limite de 3 instâncias por som, pitch ±8%
- [ ] SFX: passos, golpes, impactos, tiro, recarga, habilidades, transformação,
      dano recebido, morte, UI
- [ ] Música: menu, combate, boss
- [ ] Ambiência da arena
- [ ] Fontes CC0 (Kenney Audio, freesound CC0) com `SOURCE.md`

### 10. Cenário

- [ ] Substituir as primitivas da arena por assets modulares CC0
- [ ] Iluminação final, `WorldEnvironment` com SSAO, tonemap e bloom
- [ ] **Rebake da navmesh** após qualquer mudança de geometria
- [ ] Verificar que a legibilidade sob a câmera fixa não piorou

### 11. Notices

- [ ] `THIRD-PARTY-NOTICES.md` completo, no formato da spec 13 §9

## Critérios de aceite

- [ ] Os dois personagens usam modelos reais com animações sem deformação
- [ ] Os 5 inimigos têm modelo, animação e morte próprios
- [ ] Armas presas corretamente às mãos via `BoneAttachment3D`
- [ ] Golpes conectam **visualmente** no momento do dano
- [ ] Transformação é visualmente inconfundível
- [ ] Todo asset tem `SOURCE.md` e licença compatível
- [ ] Nenhum asset CC-BY-NC no repositório
- [ ] Trocar o `.glb` de um personagem exige só novo `AnimationSet` + `BoneMap`
- [ ] Performance mantida: 40 inimigos ≥ 60 fps **com modelos reais**

## Riscos

| Risco | Mitigação |
|---|---|
| **Retarget deformando o rig** | validar um personagem completo antes de processar todos |
| Estilos visuais inconsistentes entre packs | KayKit e Quaternius são ambos low-poly estilizados — combinam bem; unificar por material e iluminação |
| Performance caindo com modelos reais | LODs no import, orçamento de triângulos, medir de novo com 40 inimigos |
| Animações não batendo com as janelas de hit | ajustar `MeleeComboStep`, nunca o contrário |
| Milestone escorregando | é o segundo mais arriscado do projeto; cortar VFX secundário antes de cortar animação |
