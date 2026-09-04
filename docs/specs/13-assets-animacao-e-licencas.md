# 13 — Assets 3D, animação e licenças

## 1. Política de licenças

**Regra:** só entra no repositório asset com licença que permita uso comercial
sem restrição viral.

| Licença | Status | Observação |
|---|---|---|
| **CC0 / Domínio público** | ✅ preferida | sem atribuição obrigatória (damos mesmo assim) |
| **MIT / Apache-2.0** | ✅ para código | atribuição em `THIRD-PARTY-NOTICES.md` |
| **CC BY 4.0** | ⚠️ caso a caso | exige atribuição; só se realmente derivarmos algo |
| **Mixamo (Adobe)** | ✅ animações | uso permitido em jogos pessoais e comerciais |
| **CC BY-NC / NC-SA** | ❌ **proibido** | bloqueia uso comercial |
| **Asset pago sem licença clara** | ❌ | só após leitura da EULA |

Todo asset entra com um `assets/<pasta>/SOURCE.md` contendo: origem, autor, URL,
licença, data do download. Sem esse arquivo, o PR não passa.

> ⚠️ **RoboBlast/GDQuest:** o código é MIT, mas os assets artísticos são
> **CC-BY-NC-SA**. Podemos estudar o código; **nenhum modelo ou textura desse
> projeto entra no repositório**.

## 2. Fontes escolhidas para o MVP

### Quaternius — CC0

| Pack | Uso |
|---|---|
| **Ultimate Animated Character Pack** (50+ personagens animados) | base do Gunslinger e de todos os inimigos |
| **RPG Character Pack** (rigged/animated, FBX/OBJ/Blend/glTF) | variações de inimigo |
| **Animated Guns Pack** (revolver, pistol, shotgun, sniper, P90) | **revólver do Gunslinger** |

### KayKit — CC0

| Pack | Uso |
|---|---|
| **KayKit Adventurers** (5 personagens rigged/animados, espadas, escudos, machados, arco, besta; GLTF + FBX; compatibilidade declarada com Godot) | **Swordsman + espada** |

### Mixamo — Adobe

Animações complementares, retargetadas para os esqueletos acima.

### Opções pagas — avaliadas, adiadas

| Asset | Preço | Decisão |
|---|---|---|
| Neko Ninja Labs — *Female Gunner 002* (feminina, cabelo loiro, Glock, rig, animações, FBX/GLB) | US$ 22,50 | **não comprar no MVP** |
| Neko Ninja Labs — *Female Gunner 001* (loira, pistola) | US$ 30 | idem |

Correspondem melhor à descrição pedida, mas trocar o modelo depois é uma
linha de `CharacterDefinition.ModelScene`. Não faz sentido gastar antes de o
gameplay estar bom.

## 3. Mapa de assets do MVP

```
assets/
├── characters/
│   ├── swordsman/   model.glb · textures/ · SOURCE.md   (KayKit, CC0)
│   ├── gunslinger/  model.glb · textures/ · SOURCE.md   (Quaternius, CC0)
│   └── enemies/     grunt/ runner/ shooter/ brute/ warlord/
├── weapons/
│   ├── sword/       sword.glb · SOURCE.md                (KayKit, CC0)
│   └── revolver/    revolver.glb · SOURCE.md             (Quaternius, CC0)
├── animations/
│   ├── mixamo/      *.glb (biblioteca, não usada direto)
│   └── SOURCE.md
├── vfx/  materials/  audio/  fonts/  ui/
└── arena/  modular/ props/ SOURCE.md
```

## 4. Pipeline de import

### 4.1 Formato

**glTF 2.0 binário (`.glb`)** para tudo. FBX depende de conversor externo e
produz import inconsistente; converter FBX → glb no Blender **antes** de entrar
no repositório.

### 4.2 Escala e orientação

| Regra | Valor |
|---|---|
| 1 unidade Godot | 1 metro |
| Altura do personagem | ~1.8 m |
| Frente do modelo | eixo `-Z` |
| Origem | entre os pés, no chão |

Corrigir no Blender, não com `scale` no nó — escala em nó quebra física e
`BoneAttachment3D`.

### 4.3 Configuração de import no Godot

| Opção | Valor |
|---|---|
| Root type | `Node3D` |
| Skeleton | preservar, `Import as Skeleton Bones` ligado |
| Animation → Import | ligado; `Trimming` ligado |
| Animation → Loop mode | `Linear` em Idle/Walk/Run; `None` em ataques |
| Meshes → Generate LODs | ligado |
| Meshes → Create Shadow Meshes | ligado |
| Materials → Extract | extrair para `.tres` (permite editar sem reimportar) |

Os `.import` gerados **são commitados**.

### 4.4 Retarget de animações Mixamo

Godot 4 tem `BoneMap` + perfil `SkeletonProfileHumanoid`:

1. Importar o personagem, criar um `BoneMap` mapeando o esqueleto → perfil
   humanoide.
2. No import do `.glb` do personagem, definir `Retarget → Bone Map`.
3. Importar cada animação Mixamo com o **mesmo** `BoneMap`.
4. Extrair para uma `AnimationLibrary` (`animations/<personagem>.res`).
5. Referenciar os clipes no `AnimationSet.tres` por papel.

Rigs Mixamo prefixam ossos com `mixamorig:`. O `BoneMap` absorve isso — nunca
renomear osso à mão no Godot.

## 5. Lista de animações necessárias

### Comuns

`Idle` · `Walk` (blend 4 direções ou strafe) · `Run` · `Dash` ·
`Hit` · `Knockback` · `Death`

### Swordsman

`Sword_Idle` · `Slash_1` · `Slash_2` · `Slash_3` · `Heavy_Slash` ·
`Uppercut` · `Spin` · `Lunge` · `Transform_In` · `Berserker_Idle`

### Gunslinger

`Pistol_Idle` · `Fire` · `Reload` · `Fan_Fire` · `Aim_Deadeye` ·
`Roll_Shot` · `Transform_In` · `Overdrive_Idle`

### Inimigos

`Idle` · `Walk` · `Run` · `Alert` · `Attack` (1–3 variações) · `Hit` · `Death`

## 6. `AnimationTree`

`AnimationNodeStateMachine` na raiz:

```
Locomotion (BlendSpace2D: Idle ↔ Walk ↔ Run)
   ├─▶ Attack      (OneShot, sai ao terminar)
   ├─▶ Ability     (OneShot, clipe vindo do AnimationSet)
   ├─▶ Hit         (OneShot, prioridade sobre Attack)
   ├─▶ Dash
   └─▶ Death       (estado terminal)
```

- Transições com `xfade` de 0.08–0.15 s.
- Janelas de hit **não** vêm de call-method track na animação — vêm de
  `MeleeComboStep.HitWindowStart/End` em código. Motivo: trocar o modelo troca
  as animações, e tracks embutidos se perderiam junto.
- Root motion **desligado**. Deslocamento é do `MovementComponent`, sempre.

## 7. Orçamento técnico

| Item | Alvo |
|---|---|
| Triângulos — personagem jogável | ≤ 15k |
| Triângulos — inimigo comum | ≤ 6k |
| Ossos por rig | ≤ 70 |
| Texturas de personagem | 1024², atlas único, com LOD |
| Materiais por personagem | ≤ 3 |
| Draw calls na arena cheia | ≤ 900 |
| VRAM total | ≤ 1.5 GB |

Assets CC0 desses packs já ficam bem abaixo disso — são low-poly estilizados,
o que também dá coerência visual entre fontes diferentes.

## 8. Áudio

| Categoria | Buses |
|---|---|
| Master → Music, SFX, UI, Ambience | 4 buses, todos com volume nas configurações |

- SFX 3D via `AudioStreamPlayer3D` pooled (com 40 inimigos, sons soltos estouram
  o limite de vozes).
- Limite de 3 instâncias simultâneas do mesmo som, com variação de pitch de ±8%.
- Fontes CC0: Kenney Audio, freesound.org (filtrar por CC0), sfxr para
  protótipo.

## 9. `THIRD-PARTY-NOTICES.md`

Mantido na raiz, atualizado a cada asset novo:

```
## Quaternius — Ultimate Animated Character Pack
Autor: Quaternius · https://quaternius.com · CC0 · baixado em AAAA-MM-DD
Uso: modelo do Gunslinger e inimigos.

## KayKit — Adventurers
Autor: Kay Lousberg · https://kaylousberg.itch.io · CC0 · baixado em AAAA-MM-DD
Uso: modelo do Swordsman e espada.

## Mixamo (Adobe)
https://mixamo.com · uso permitido em projetos pessoais e comerciais.
Uso: animações de locomoção e combate, retargetadas.
```

## 10. Critérios de aceite (M8)

- [ ] Todo asset tem `SOURCE.md` com licença registrada.
- [ ] Nenhum asset CC-BY-NC no repositório (auditável por grep nos `SOURCE.md`).
- [ ] Os dois personagens usam animações retargetadas do Mixamo sem
      deformação visível.
- [ ] Trocar o `.glb` do Swordsman por outro modelo exige apenas novo
      `AnimationSet` + `BoneMap`.
- [ ] `THIRD-PARTY-NOTICES.md` completo antes de qualquer distribuição.
