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

### KayKit — CC0 (integrado no ticket 34)

| Pack | Uso |
|---|---|
| **Adventurers Character Pack 2.0** | Knight do Swordsman e Rogue sem capuz da Gunslinger |
| **Character Animations 1.1** | bancos de movimento, combate, dano, morte e transformação para os dois rigs |

Os sete bancos importados e as licenças estão registrados em
`assets/animations/kaykit/SOURCE.md`; cada modelo tem seu próprio `SOURCE.md`.

### Quaternius — CC0 (opção futura)

| Pack | Uso |
|---|---|
| **Ultimate Animated Character Pack** (50+ personagens animados) | possível opção para inimigos |
| **RPG Character Pack** (rigged/animated, FBX/OBJ/Blend/glTF) | variações de inimigo |
| **Animated Guns Pack** (revolver, pistol, shotgun, sniper, P90) | possível modelo futuro de arma |

Mixamo e RPG Character Pack permanecem alternativas para conteúdo futuro; não
há arquivos deles incorporados hoje.

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
│   ├── swordsman/   model.glb · SOURCE.md (Knight, KayKit CC0)
│   └── gunslinger/  model.glb · SOURCE.md (Rogue, KayKit CC0)
└── animations/
    └── kaykit/     bancos *.glb · SOURCE.md (KayKit CC0)

scenes/weapons/      cenas procedurais para espada, revólver e braço-canhão
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
| Skeleton | preservar o nó `Skeleton3D`; `Import as Skeleton Bones` desligado para manter esse nó acessível a animações e `BoneAttachment3D` |
| Animation → Import | ligado; `Trimming` desligado nos bancos KayKit para preservar o tempo original dos clipes |
| Animation → Loop mode | `Linear` em Idle/Walk/Run; `None` em ataques |
| Meshes → Generate LODs | ligado |
| Meshes → Create Shadow Meshes | ligado |
| Materials → Extract | desligado para os GLBs KayKit; materiais importados permanecem embutidos no asset |

Os `.import` gerados **são commitados**.

### 4.4 Rig compartilhado KayKit

Os modelos Knight e Rogue e os bancos de animação KayKit compartilham os mesmos
nomes de 23 ossos; os clipes são copiados para uma `AnimationLibrary` por
personagem e suas trilhas apontam ao `Skeleton3D` importado. Nenhum retarget
manual ou renomeação de osso é necessário.

Para rigs futuros de outra fonte, Godot 4 tem `BoneMap` + perfil
`SkeletonProfileHumanoid`:

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

Para os dois personagens jogáveis, o `AnimationTree` usa um `AnimationNodeBlendTree`:

```
Locomotion (BlendSpace1D: Idle ↔ Walk ↔ Run)
  → Attack → Ability → Reload → Jump → Fall → Land → Dash → Dive
  → Hit → Transform → Death
```

- Os OneShots fazem transição com `xfade` de 0.1 s.
- A velocidade horizontal normalizada dirige o `BlendSpace1D`.
- Os eventos de jogo escolhem o clipe e disparam o OneShot correspondente.
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

Mantido na raiz, atualizado a cada asset novo. Registre somente fontes
efetivamente incorporadas; fontes futuras ficam na seção separada do próprio
arquivo. Hoje, a tabela de assets incorporados registra:

```
KayKit Adventurers Character Pack 2.0 — Kay Lousberg — CC0 1.0
Uso: Knight do Swordsman e Rogue da Gunslinger.

KayKit Character Animations 1.1 — Kay Lousberg — CC0 1.0
Uso: animações dos dois personagens jogáveis no rig compartilhado.
```

## 10. Critérios de aceite (M8)

- [ ] Todo asset tem `SOURCE.md` com licença registrada.
- [ ] Nenhum asset CC-BY-NC no repositório (auditável por grep nos `SOURCE.md`).
- [ ] Os dois personagens usam as animações KayKit do rig compartilhado sem
      deformação visível.
- [ ] Trocar o `.glb` do Swordsman exige um novo `AnimationSet`; usar um rig
      diferente também exige um `BoneMap` compatível.
- [ ] `THIRD-PARTY-NOTICES.md` completo antes de qualquer distribuição.
