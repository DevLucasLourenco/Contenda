# 03 — Input, comandos e combos

O sistema de habilidades por **sequência de WASD + M2** é a mecânica assinatura
do jogo. Ele precisa ser um sistema de verdade — não `if (W && W)` espalhado.

## 1. Mapa de entrada (`InputMap`)

| Ação (`StringName`) | Padrão | Função |
|---|---|---|
| `move_up` / `move_down` / `move_left` / `move_right` | W / S / A / D | movimento **e** tokens de comando |
| `attack_basic` | M1 | ataque básico da arma |
| `command_confirm` | M2 | confirma a sequência → executa habilidade |
| `form_prev` / `form_next` | Scroll ↑ / Scroll ↓ | **seleciona** transformação |
| `form_activate` | M3 (botão da roda) | **ativa/desativa** transformação |
| `pause` | Esc | pause |
| `dodge` | Space | reservado (pós-MVP) |
| `ui_accept` / `ui_cancel` | Enter / Esc | navegação de menu |

Todas as ações são declaradas em `project.godot` e referenciadas por constantes
em `GameConstants.InputActions` — nunca por string literal no código.

Rebind: qualquer ação da tabela acima é remapeável, exceto `pause` e as `ui_*`.
Ver [spec 14](14-configuracoes-persistencia-e-build.md).

## 2. `IntentFrame` — separando leitura de decisão

```csharp
public readonly record struct IntentFrame(
    Vector2 Move,             // eixo bruto, já normalizado
    Vector3 AimPoint,
    bool    AttackPressed,
    bool    ConfirmPressed,
    int     FormScrollDelta,  // -1 / 0 / +1
    bool    FormActivatePressed);
```

- `PlayerInputController` produz um `IntentFrame` por tick de física.
- `EnemyBrain` produz o **mesmo tipo**, sem teclado.
- `MovementComponent` e `CombatComponent` consomem `IntentFrame` e não sabem se
  a origem é humana ou IA. É isso que evita duplicar locomoção para inimigos.

## 3. Convivência entre movimento e comandos

WASD faz duas coisas ao mesmo tempo. A regra que resolve o conflito:

> **Movimento lê o estado contínuo (`IsActionPressed`). O buffer de comandos lê
> apenas a borda de subida (`IsActionJustPressed`).**

Consequências:

- Andar segurando `W` grava **1 token**, não 60 por segundo.
- Tocar `W`, `W` rapidamente grava `[W, W]` e o personagem mal se move — o que é
  exatamente a leitura desejada.
- Nunca bloqueamos o movimento para "entrar em modo comando". O jogador digita
  enquanto reposiciona.

Corolário de design: sequências devem ser curtas (2–3 tokens). Sequências longas
forçam o jogador a ficar parado, o que é ruim num modo horda.

## 4. `CommandToken`

```csharp
public enum CommandDirection : byte { Up, Down, Left, Right }

public readonly record struct CommandToken(
    CommandDirection Direction,
    float TimestampSeconds);
```

## 5. `CommandBuffer`

Buffer circular de tamanho fixo, com expiração por tempo.

```csharp
public sealed class CommandBuffer   // POCO — testável sem Godot
{
    public int   Capacity        { get; init; } = 6;
    public float TokenLifetime   { get; init; } = 0.70f; // s por token
    public float SequenceTimeout { get; init; } = 1.20f; // s desde o 1º token

    public void  Push(CommandDirection dir, float now);
    public void  Tick(float now);        // expira tokens
    public void  Clear();
    public ReadOnlySpan<CommandDirection> Current { get; }
    public event Action<ReadOnlyMemory<CommandDirection>> Changed;
}
```

### Regras de expiração

| Situação | Efeito |
|---|---|
| token mais antigo que `TokenLifetime` | é descartado (janela deslizante) |
| primeiro token mais antigo que `SequenceTimeout` | buffer inteiro limpo |
| buffer cheio (`Capacity`) | descarta o mais antigo (FIFO) |
| habilidade executada com sucesso | buffer limpo |
| M2 pressionado **sem** match | buffer limpo + feedback de falha |
| personagem morre / entra em stun / pausa | buffer limpo |

> **Nota de tuning:** `TokenLifetime` = 0.70 s é o valor inicial. Se em playtest
> o jogo parecer "engolir inputs", subir para 0.9 antes de mexer no resolver.

## 6. `AbilityComboResolver`

Constrói uma **trie** (árvore de prefixos) a partir das `AbilityDefinition` do
personagem equipado, uma vez, no `Configure`.

```csharp
public sealed class AbilityComboResolver
{
    public AbilityComboResolver(IReadOnlyList<AbilityDefinition> abilities);

    /// Match exato no nó atual da trie (null se não há habilidade ali).
    public AbilityDefinition Resolve(ReadOnlySpan<CommandDirection> seq);

    /// Habilidades cujo prefixo é `seq` — alimenta o HUD ao vivo.
    public IReadOnlyList<AbilityDefinition> Candidates(
        ReadOnlySpan<CommandDirection> seq);
}
```

### Ambiguidade e prefixos

`[W,W]` (Dash Slash) é prefixo de `[W,W,A]` (hipotética). A regra:

> **M2 confirma o match exato da sequência atual.** Se o nó atual tem uma
> habilidade, ela dispara — mesmo que existam continuações possíveis. Sequências
> mais longas exigem digitar os tokens antes do M2.

Isso mantém o input determinístico e sem janela de espera. O custo é de design:
não criar uma sequência longa cujo prefixo já seja uma habilidade **do mesmo
personagem**, a menos que seja intencional (upgrade natural do combo).

Validação: `AbilityComboResolver` **lança em construção** se duas habilidades do
mesmo personagem declararem a mesma sequência. Isso vira erro em tempo de carga,
não bug silencioso.

## 7. Fluxo completo

```
t=0ms     W pressionado   -> Push(Up)      buffer = [W]
t=180ms   W pressionado   -> Push(Up)      buffer = [W, W]
t=350ms   M2 pressionado
              |
              v
   AbilityComboResolver.Resolve([W, W])  ->  DashSlash.tres
              |
              v
   AbilityComponent.TryExecute(DashSlash)
       ├─ cooldown pronto?      não -> feedback "em recarga", buffer limpo
       ├─ mana suficiente?      não -> feedback "sem mana",   buffer limpo
       ├─ estado permite?       não -> feedback,              buffer limpo
       └─ sim -> consome mana, inicia cooldown, executa, limpa buffer
```

Se `Resolve` retorna `null`: buffer limpo, som curto de falha, flash vermelho na
guia de combos. **Nunca** cair no ataque básico como fallback — isso mascara o
erro do jogador e confunde o aprendizado.

## 8. Prioridade de input no mesmo frame

Se M1 e M2 chegarem no mesmo tick de física:

1. `command_confirm` (M2) — tem prioridade
2. `attack_basic` (M1)
3. `form_activate` (M3)

Motivo: a habilidade é a ação de maior investimento (custou uma sequência).

## 9. Bloqueios de input

`CombatComponent` expõe `ActionLock` (flags):

| Flag | Bloqueia |
|---|---|
| `Movement` | `MovementComponent` ignora `Move` (ainda aplica gravidade/knockback) |
| `Rotation` | `TargetingComponent` não gira o personagem |
| `BasicAttack` | M1 ignorado |
| `Abilities` | M2 ignorado (buffer continua acumulando) |
| `Forms` | scroll e M3 ignorados |

Durante uma habilidade, os locks vêm da própria `AbilityDefinition`
(ver [spec 05](05-habilidades.md)). Durante hitstun, `Movement | BasicAttack |
Abilities`.

## 10. Gamepad (pós-MVP, mas o desenho já contempla)

| Ação | Gamepad |
|---|---|
| movimento | analógico esquerdo |
| mira | analógico direito |
| tokens de comando | **D-pad** (não o analógico — precisa de borda discreta) |
| `attack_basic` | RT |
| `command_confirm` | RB |
| `form_prev/next` | LB / LT |
| `form_activate` | L3 |

O D-pad emite exatamente os mesmos `CommandDirection`; o resolver não muda.

## 11. Critérios de aceite (M3)

- [ ] Andar segurando W por 3 s grava **1** token.
- [ ] `[W,W]` + M2 executa Dash Slash em < 1 frame de latência perceptível.
- [ ] Sequência incompleta expira em ~1.2 s e o HUD volta ao estado neutro.
- [ ] Duas habilidades com a mesma sequência quebram o carregamento com
      mensagem clara.
- [ ] `CommandBuffer` e `AbilityComboResolver` têm testes xUnit cobrindo
      expiração, capacidade, prefixo, ambiguidade e limpeza.
