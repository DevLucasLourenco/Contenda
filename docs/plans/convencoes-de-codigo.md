# Convenções de código

C# em Godot 4. Regras curtas, aplicáveis em code review.

## 1. Nomenclatura

| Item | Padrão | Exemplo |
|---|---|---|
| Classe, método, propriedade | `PascalCase` | `AbilityComponent`, `TryConsume` |
| Campo privado | `_camelCase` | `_currentHealth` |
| Constante | `PascalCase` | `MaxBufferSize` |
| Parâmetro, local | `camelCase` | `abilityId` |
| Interface | `I` + `PascalCase` | `IWeapon` |
| Enum | singular; `[Flags]` plural | `EnemyState`, `ActionLock` |
| Arquivo `.cs` | nome da classe pública | `AbilityComponent.cs` |
| Cena `.tscn` | `PascalCase` | `CharacterSelectMenu.tscn` |
| Resource `.tres` | `snake_case` | `dash_slash.tres` |
| Ação de input | `snake_case` | `command_confirm` |

Namespace = caminho da pasta: `src/Components/Abilities/` →
`Contenda.Components.Abilities`.

## 2. Godot em C#

### Sempre

```csharp
public partial class Foo : Node    // "partial" é obrigatório em toda classe Node
```

- `[Export]` para o que um designer ajusta; `private` para o resto.
- `[GlobalClass]` em todo `Resource` de conteúdo.
- `StringName` para ids, ações e nomes de animação — nunca `string` em hot path.
- `%UniqueName` ou `[Export] NodePath` para achar nós. Nunca caminho literal.
- Cachear referências de nó no `_Ready`.

### Nunca

- `GetNode()` fora do `_Ready`.
- `GetNode("../../Player")` — caminho relativo subindo a árvore.
- `FindChild` em runtime.
- Signal do Godot para lógica de gameplay entre componentes C# (usar `event`).
- `Godot.Collections.Array/Dictionary` fora de `[Export]` — usar coleções .NET.
- Herdar de `Node` uma classe cuja lógica você quer testar.

## 3. Ciclo de vida

```csharp
public override void _Ready()
{
    _health = ctx.Health;
    _health.Damaged += OnDamaged;     // assinar
}

public override void _ExitTree()
{
    _health.Damaged -= OnDamaged;     // desassinar — SEMPRE
}
```

**Regra sem exceção:** todo `+=` tem um `-=`. Em nós pooled, a desassinatura
acontece no `ResetForSpawn()`, porque `_ExitTree` não é chamado.

Ordem: `_EnterTree` → `_Ready` (filhos antes dos pais) → `_Process` /
`_PhysicsProcess` → `_ExitTree`.

## 4. Física vs. render

| Faça em `_PhysicsProcess` | Faça em `_Process` |
|---|---|
| ler input (`IsActionJustPressed`) | câmera |
| movimento, `MoveAndSlide` | UI/HUD |
| IA, navegação | VFX puramente visual |
| dano, cooldowns, dreno de mana | interpolação visual |

Ler `IsActionJustPressed` em `_Process` **engole inputs** quando o framerate é
alto. É um bug real, não teoria.

## 5. Performance

Proibido em `_Process` / `_PhysicsProcess`:

- `new` (usar pooling, `Span`, `ArrayPool`)
- LINQ
- concatenação de `string`
- `GetNode`, `FindChild`
- `foreach` sobre `Godot.Collections`
- boxing de `Variant`

```csharp
// ❌
var alvos = inimigos.Where(e => e.IsAlive).OrderBy(e => e.Distance).ToList();

// ✅
for (int i = 0; i < _enemies.Count; i++) { ... }   // buffer reutilizado
```

## 6. Nullability

- `<Nullable>enable</Nullable>` no `.csproj`.
- Componentes opcionais do `CharacterContext` são anotados `?` e checados.
- `ArgumentNullException.ThrowIfNull` na fronteira pública.
- Nada de `!` (null-forgiving) sem um comentário justificando.

## 7. Estrutura de classe

```csharp
public sealed partial class AbilityComponent : Node, ICharacterComponent
{
    // 1. constantes
    // 2. [Export]
    // 3. campos privados
    // 4. propriedades públicas
    // 5. eventos
    // 6. ciclo de vida (_Ready, _ExitTree, _PhysicsProcess)
    // 7. API pública
    // 8. helpers privados
}
```

`sealed` por padrão. Herança só quando há polimorfismo real (`WeaponBase`).

## 8. Comentários

Comente **por quê**, não **o quê**.

```csharp
// ❌ incrementa o índice do combo
_comboIndex++;

// ✅ o cooldown começa no cast, não no hit, para casar com o HUD
_cooldown.Start(def.Cooldown);
```

XML doc só em API pública não óbvia. Nada de comentário decorativo.

## 9. Tratamento de erro

- Erro de **conteúdo** (`.tres` inválido): falha alto no boot em debug, log em
  release. Ver `ContentValidator`.
- Erro de **estado** que não deveria acontecer: `Debug.Assert` + log.
- Nunca engolir exceção em silêncio.
- Nada de `try/catch` em hot path.

## 10. Git

- Branch por milestone: `feature/m3-abilities`; PRs pequenos dentro dele.
- Commits imperativos em português ou inglês — escolher um e manter.
- Um commit por assunto. **Balanceamento nunca no mesmo commit que refactor.**
- `.tres` alterado por balanceamento: o corpo do commit traz antes/depois.

## 11. Checklist de review

- [ ] compila em `ExportRelease` sem warning
- [ ] nenhum nó 2D novo no gameplay
- [ ] nenhum valor de balanceamento hardcoded em `.cs`
- [ ] todo `+=` tem `-=` correspondente
- [ ] nada alocado em `_PhysicsProcess`
- [ ] `[Export]` no que o designer precisa ajustar
- [ ] testes para lógica pura nova
- [ ] spec atualizada se o comportamento mudou
- [ ] navmesh rebakeada se a geometria mudou
- [ ] asset novo com `SOURCE.md` e licença compatível
