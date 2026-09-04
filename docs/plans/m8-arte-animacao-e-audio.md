# M8 — Arte, animação e áudio

**Objetivo:** trocar cápsulas e primitivas por modelos reais animados, com VFX
e áudio. O gameplay já está fechado — aqui ele ganha corpo.

**Esforço:** 8–12 dias · **Depende de:** M7

Spec: [13 — Assets, animação e licenças](../specs/13-assets-animacao-e-licencas.md)

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

- [ ] Baixar **KayKit Adventurers** (CC0) → Swordsman + espada
- [ ] Baixar **Quaternius Ultimate Animated Character Pack** (CC0) →
      Gunslinger + inimigos
- [ ] Baixar **Quaternius Animated Guns Pack** (CC0) → revólver
- [ ] Criar `SOURCE.md` em cada pasta: autor, URL, licença, data
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

- [ ] `BoneMap` para cada esqueleto, mapeando para `SkeletonProfileHumanoid`
- [ ] Import dos clipes Mixamo com o mesmo `BoneMap`
- [ ] Extrair para `AnimationLibrary` por personagem
- [ ] Verificar dedos, ombros e quadril — os pontos que costumam quebrar

### 5. `AnimationSet`

- [ ] `animset_swordsman`, `animset_gunslinger`, `animset_berserker`,
      `animset_overdrive`, `animset_grunt`, … (spec 12 §4)
- [ ] Mapear todos os papéis, incluindo `AbilityAnimations` por `abilityId`
- [ ] Nenhum nome de clipe hardcoded em `.cs`

### 6. `AnimationTree`

- [ ] `AnimationNodeStateMachine` conforme spec 13 §6
- [ ] `BlendSpace2D` de locomoção (Idle ↔ Walk ↔ Run)
- [ ] OneShots para Attack, Ability, Hit, Dash
- [ ] `Death` terminal
- [ ] `xfade` de 0.08–0.15 s
- [ ] **Root motion desligado** — deslocamento é do `MovementComponent`
- [ ] `CharacterAnimator` traduzindo estado de gameplay em parâmetros do tree

### 7. Sincronização de combate

- [ ] Ajustar `HitWindowStart/End` de cada `MeleeComboStep` às animações reais
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
