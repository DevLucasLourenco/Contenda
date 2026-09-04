# 16 — Mobilidade, críticos e combate aéreo

Esta spec adiciona **pulo, dash, golpes críticos e combate aéreo**, e redefine as
duas transformações. Ela altera as specs
[02](02-camera-e-mundo-25d.md), [03](03-input-comandos-e-combos.md),
[04](04-atributos-vida-mana-stats.md), [06](06-transformacoes.md),
[07](07-combate-armas-e-dano.md) e [08](08-personagens.md) — as mudanças estão
marcadas na §7.

## 1. Por que isto encaixa em vez de inchar

O acréscimo não é solto: ele fecha um circuito que já existia pela metade.
`Rising Slash` sempre lançou o inimigo para o alto e não havia o que fazer com
isso — o alvo subia e caía sozinho. Com pulo e golpe aéreo, lançar vira
**preparação**, e o espadachim ganha um combo vertical de verdade:

```
S W + M2          Espaço          M1  M1            M1 (segurando queda)
Rising Slash  →   perseguir  →   golpes aéreos  →   estocada de queda
lança o alvo      no ar           mantêm no ar       impacto em área ao aterrissar
```

O dash faz o mesmo pelo lado defensivo: a arena tem inimigos que telegrafam
golpes, e até agora a única resposta era andar para trás. Dash dá uma resposta
com custo (recarga) e recompensa (invulnerabilidade breve).

## 2. Input revisado

| Ação | Tecla | Muda o quê |
|---|---|---|
| `dash` | **Shift** | novo |
| `jump` | **Espaço** | novo |
| ~~`dodge`~~ | ~~Espaço~~ | **removido** — o dash o substitui |

Shift e Espaço **não** emitem símbolos de comando: não são direcionais, então
não interferem no buffer da [spec 03](03-input-comandos-e-combos.md). Dá para
pular no meio de uma sequência sem perdê-la — e isso é desejável, porque
sequências de três símbolos exigem reposicionamento.

`IntentFrame` ganha dois campos:

```csharp
public readonly record struct IntentFrame(
    Vector2 Move, Vector3 AimPoint,
    bool AttackPressed, bool ConfirmPressed,
    int FormScrollDelta, bool FormActivatePressed,
    bool JumpPressed,        // ← novo
    bool DashPressed);       // ← novo
```

Como a IA preenche o mesmo `IntentFrame`, inimigos passam a poder pular e
avançar com dash sem nenhum código novo de locomoção — o `brute` usa dash como
investida.

## 3. Pulo

### Parâmetros

```csharp
// MovementSettings
[Export] public float JumpHeight        = 2.2f;   // m — altura de pico
[Export] public float AirControlFactor  = 0.65f;  // 0..1 do controle no solo
[Export] public float CoyoteTime        = 0.12f;  // s após sair da borda
[Export] public float JumpBufferTime    = 0.12f;  // s antes de tocar o chão
[Export] public float FallGravityScale  = 1.6f;   // queda mais rápida que a subida
[Export] public int   MaxAirJumps       = 0;      // 1 só no Berserker
```

`JumpVelocity` é derivada, não configurada: `sqrt(2 · g · JumpHeight)`. Assim o
designer ajusta **altura em metros**, que é o que ele consegue visualizar, e não
uma velocidade abstrata.

### Regras de sensação

- **Coyote time:** pular até 0.12 s depois de sair de uma plataforma ainda
  funciona. Sem isso, saltar de quina parece que o jogo engoliu o input.
- **Jump buffer:** apertar Espaço até 0.12 s antes de aterrissar pula assim que
  tocar o chão.
- **Queda mais pesada que a subida** (`FallGravityScale`): o pulo flutuante é o
  erro mais comum em jogos 3D, e a câmera fixa o exagera.
- **Sem pulo duplo no estado base.** O Berserker concede um (`MaxAirJumps = 1`).

### Efeito na câmera — não ignorar

Com pulo, seguir a posição do jogador em Y faz a câmera balançar a cada salto,
e sob ângulo fixo isso enjoa rápido. A regra:

> O `CameraRig` segue **X e Z com `FollowSmoothTime` normal (0.16 s)** e **Y com
> um tempo muito mais longo (0.55 s)**, ignorando variações de altura menores
> que a altura de um pulo enquanto o jogador estiver no ar.

Quando o jogador **aterrissa em outro nível** (praça, telhado), o Y alvo muda de
patamar e a câmera acompanha em ~0.5 s. `CameraSettings` ganha
`VerticalFollowSmoothTime` e `VerticalDeadZone`.

## 4. Dash

| Parâmetro | Valor |
|---|---|
| Distância | 5.0 m |
| Duração | 0.18 s |
| Recarga | 1.2 s |
| Invulnerabilidade | 0.12 s, começando no quadro 1 |
| Custo de mana | **nenhum** |
| No ar | permitido, uma vez por pulo |

**Por que dash não custa mana:** mana é a moeda de habilidades e transformações.
Se o dash também cobrasse, o jogador escolheria entre se defender e se
transformar — e escolheria sempre defender, matando o sistema de formas. A
recarga é o custo.

Direção: a do input de movimento; sem input, a direção que o personagem encara.
Durante o dash, `ActionLock.Rotation` está ativo — não dá para curvar no meio.

Sai de: hitstun (é o escape), recovery de ataque básico.
Não sai de: execução de habilidade, atordoamento pesado.

## 5. Golpes críticos

### Novos atributos

```csharp
public enum StatId : byte
{
    // … existentes …
    CritChance,       // 0..1
    CritMultiplier,   // multiplicador do dano no acerto crítico
}
```

| Personagem | `CritChance` base | `CritMultiplier` base |
|---|---|---|
| Swordsman | **0.10** | 2.0 |
| Gunslinger | 0.05 | 1.8 |
| Inimigos | 0.00 | — |

O sorteio acontece **uma vez por golpe**, não por alvo: um `Spin Slash` que
acerta cinco inimigos é crítico em todos ou em nenhum. Isso evita que ataques
de área virem loteria e torna o crítico legível.

`DamageInfo.IsCritical` já existia na [spec 07](07-combate-armas-e-dano.md) e
passa a ser preenchido de fato.

### Feedback — obrigatório

Um crítico invisível não existe para o jogador.

| Canal | Normal | Crítico |
|---|---|---|
| Número de dano | branco, 1.0× | **âmbar, 1.6× de tamanho, com brilho** |
| Hitstop | 0.04 s | **0.09 s** |
| Screen shake | 0.15 | 0.25 |
| Som | corte padrão | camada extra aguda |
| Partícula de impacto | padrão | faísca maior |

## 6. Combate aéreo

### Estados

`CombatComponent` passa a distinguir **solo** e **ar**. `AnimationTree` ganha os
estados correspondentes.

### Ataque aéreo (M1 no ar)

- Cadeia de **dois** golpes, mais rápida e mais fraca que a de solo
  (0.70× do dano), com alcance menor.
- Cada acerto aplica um **impulso vertical pequeno no alvo e no atacante**
  (0.8 m/s), o que sustenta o combo no ar — é o que transforma `Rising Slash`
  numa abertura de verdade.
- Gravidade reduzida a 0.35× durante a janela ativa: o golpe "segura" o
  personagem no ar sem virar voo.

### Estocada de queda (M1 no ar, segurando após o pico)

- O personagem mergulha na diagonal até o chão.
- Ao aterrissar: dano em **área de 3.5 m**, com repulsão radial.
- Dano 1.4× do golpe pesado de solo; **sempre crítico se acertar um alvo aéreo**.
- Recovery de 0.4 s ao aterrissar — é o risco que equilibra o poder.

### Inimigos no ar

Um inimigo lançado entra em `Airborne`, um estado novo da máquina da
[spec 09](09-inimigos-e-ia.md): sem navegação, sem ataque, sujeito a gravidade,
voltando a `Staggered` ao tocar o chão. Sem isso, o inimigo lançado continuaria
tentando andar no ar.

**Limite de juggle:** cada inimigo tem um contador de acertos aéreos; após
**4**, os impulsos verticais deixam de aplicar e ele cai. Sem esse teto, um
inimigo fica preso no ar indefinidamente e o modo horda vira exibição.

## 7. Transformações redefinidas

`TransformationDefinition` ganha três campos:

```csharp
[Export] public WeaponDefinition WeaponOverride;   // troca a arma equipada
[Export] public float CritChanceBonus  = 0f;       // somado ao CritChance
[Export] public int   ExtraAirJumps    = 0;
```

`WeaponOverride` é a peça que faltava: uma forma podia trocar malha e material,
mas não **o que o ataque básico faz**. Com ele, transformar-se muda a arma — e a
troca reverte junto com o resto, pelo mesmo `RemoveBySource`.

### Berserker — Swordsman

> **Fantasia:** o espadachim vira uma ameaça vertical. Corta mais fundo, corta
> mais vezes, e o céu deixa de ser um lugar seguro.

| Campo | Valor |
|---|---|
| Ativação / dreno | 20 mana · 4/s |
| `DamageMultiplier` | ×1.60 |
| `SpeedMultiplier` | ×1.15 |
| `DefenseMultiplier` | ×0.80 |
| **`CritChanceBonus`** | **+0.35** → 45 % de crítico |
| **`ExtraAirJumps`** | **+1** (pulo duplo) |
| `WeaponOverride` | `sword_berserker.tres` — mesma cadeia, +1 golpe aéreo |
| Visual | emissivo vermelho, rastro de lâmina, partículas nos ombros |

A arma da forma acrescenta um **terceiro golpe aéreo** e reduz o recovery da
estocada de queda para 0.25 s. É o que concretiza "golpes de ar".

### Overdrive — Gunslinger

> **Fantasia:** a pistoleira larga o revólver e o braço vira arma. Deixa de
> mirar em alvos e passa a mirar em espaços.

| Campo | Valor |
|---|---|
| Ativação / dreno | 25 mana · 5/s |
| **`WeaponOverride`** | **`arm_cannon.tres`** |
| `DamageMultiplier` | ×1.10 |
| `SpeedMultiplier` | ×1.15 |
| `DefenseMultiplier` | ×0.90 |
| `CritChanceBonus` | +0.05 |
| Visual | braço direito substituído por canhão; overlay ciano; rastro de movimento |

**`arm_cannon.tres`:**

| Campo | Revólver | Braço-canhão |
|---|---|---|
| Tipo | hitscan | projétil explosivo |
| Dano direto | 18 | 26 |
| **Área** | — | **2.5 m, 60 % do dano na borda** |
| Intervalo | 0.30 s | 0.50 s |
| Munição | 6 + recarga 1.6 s | **infinita, sem recarga** |
| Alcance | 25 m | 20 m |

A troca inverte o arquétipo dentro da forma: de precisão rápida para área lenta.
Perder a recarga é parte da sensação de poder — e o dreno de mana é o
contrapeso, porque a forma agora **é** a arma.

> **Nota de balanceamento:** o Overdrive antigo dava +50 % de cadência. Esse
> bônus **sai**, porque somado a dano em área tornaria a forma dominante. A
> cadência do braço-canhão é deliberadamente mais lenta que a do revólver.

## 8. O que muda nas specs existentes

| Spec | Mudança |
|---|---|
| [02](02-camera-e-mundo-25d.md) | follow vertical separado; arena com três níveis; regra de oclusão |
| [03](03-input-comandos-e-combos.md) | `dash` em Shift, `jump` em Espaço, `dodge` removido; `IntentFrame` com dois campos |
| [04](04-atributos-vida-mana-stats.md) | `CritChance` e `CritMultiplier` no `StatBlock` |
| [06](06-transformacoes.md) | `WeaponOverride`, `CritChanceBonus`, `ExtraAirJumps`; as duas formas redefinidas |
| [07](07-combate-armas-e-dano.md) | crítico no pipeline de dano; estados aéreos; estocada de queda |
| [08](08-personagens.md) | crítico base por personagem; `arm_cannon` e `sword_berserker` |
| [09](09-inimigos-e-ia.md) | estado `Airborne`; teto de juggle; `brute` com dash |
| [12](12-dados-resources-e-conteudo.md) | dois `WeaponDefinition` novos |

## 9. Critérios de aceite

- [ ] Espaço pula com coyote time e jump buffer perceptíveis
- [ ] A câmera não balança a cada pulo, mas acompanha ao mudar de nível
- [ ] Shift avança 5 m com invulnerabilidade breve, e serve de escape de hitstun
- [ ] Dash não consome mana
- [ ] Críticos são inconfundíveis — número âmbar, hitstop maior, som distinto
- [ ] Um `Spin Slash` crítico é crítico em todos os alvos, nunca em alguns
- [ ] `Rising Slash` → pulo → golpes aéreos → estocada de queda funciona como
      combo contínuo
- [ ] Nenhum inimigo passa de 4 acertos aéreos consecutivos
- [ ] Berserker eleva o crítico a ~45 % e concede pulo duplo
- [ ] Overdrive **troca a arma**: tiros passam a explodir em área e a recarga some
- [ ] Reverter qualquer forma devolve arma, atributos e pulos ao estado base
