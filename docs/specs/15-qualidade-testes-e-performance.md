# 15 — Qualidade, testes e performance

## 1. A camada testável

Testar dentro da engine é lento e frágil. Por isso a arquitetura separa
**regras puras** de **integração com o Godot**:

| Camada | Depende do Godot? | Como testar |
|---|---|---|
| `CommandBuffer`, `AbilityComboResolver` | ❌ | xUnit |
| `StatBlock`, cálculo de dano/mitigação | ❌ | xUnit |
| `AbilityCooldown`, gates de execução | ❌ | xUnit |
| Progressão de `WaveDirector` | ❌ (com relógio injetado) | xUnit |
| Pontuação, multiplicador de combo | ❌ | xUnit |
| `HealthComponent`, `ManaComponent` | ⚠️ `Node`, lógica extraível | xUnit sobre a classe interna |
| Movimento, física, navegação | ✅ | GdUnit4 |
| Cenas, HUD, pooling | ✅ | GdUnit4 |

**Regra de projeto:** toda classe cuja lógica você quer testar não deve herdar
de `Node`. Componentes `Node` são cascas finas sobre POCOs.

Exemplo:

```csharp
// POCO — testável
public sealed class HealthState
{
    public float Max, Current;
    public DamageResult Apply(in DamageInfo info, float defense) { ... }
}

// Node — casca
public sealed partial class HealthComponent : Node, IDamageable
{
    private readonly HealthState _state = new();
    public void ApplyDamage(in DamageInfo info) => _queue.Enqueue(info);
}
```

## 2. Testes mínimos por milestone

| Milestone | Testes obrigatórios |
|---|---|
| M0 | build passa, `dotnet test` roda com ≥ 1 teste |
| M2 | dano, mitigação, morte idempotente, `TryConsume` atômico, modificadores reversíveis |
| M3 | expiração de token, capacidade do buffer, match exato, prefixo, ambiguidade, gates de habilidade |
| M4 | ativar/reverter devolve stats ao base exato; dreno esgota e reverte |
| M5 | `ResetForSpawn` deixa o inimigo idêntico ao novo (GdUnit4, 100 ciclos) |
| M6 | `WaveDirector` avança com relógio simulado; fallback do inimigo preso |
| M7 | fluxo de telas sem vazar nó; settings round-trip |
| M9 | smoke test: partida completa headless até a onda 5 |

Meta de cobertura: **≥ 80% no núcleo POCO**. Não perseguir cobertura em código
de cena — o retorno é baixo.

## 3. Orçamento de performance

Máquina de referência: CPU 4 núcleos ~3 GHz, GPU classe GTX 1060 / Vega 8,
16 GB RAM, 1080p.

| Cenário | Alvo |
|---|---|
| Arena vazia | ≥ 200 fps |
| 10 inimigos | ≥ 144 fps |
| **40 inimigos + VFX (pior caso)** | **≥ 60 fps** |
| Frame time no pior caso | ≤ 16.6 ms |

### Divisão do frame (pior caso, 16.6 ms)

| Sistema | Orçamento |
|---|---|
| Render | 8.0 ms |
| Física + colisão | 2.5 ms |
| IA (brain + navegação) | 3.0 ms |
| Animação (`AnimationTree` × 40) | 2.0 ms |
| Gameplay (componentes, habilidades) | 0.8 ms |
| UI | 0.3 ms |

### Regras anti-regressão

1. **Zero alocação por frame** no hot path. Nada de `new` em `_PhysicsProcess`;
   `Span`/`ArrayPool` onde precisar de buffer temporário.
2. **Sem `GetNode`/`FindChild` por frame.** Tudo cacheado no `_Ready`.
3. **Sem LINQ** em `_Process`/`_PhysicsProcess`.
4. **Sem `string`** em hot path — `StringName` sempre.
5. **Pooling** para inimigos, projéteis, números de dano, VFX e vozes de áudio.
6. **Timers escalonados** — repath, perception e LOD de IA com offset de fase.

### Ferramentas

- Profiler do Godot (Debugger → Profiler / Visual Profiler).
- Overlay `--stats` com FPS, frame time, draw calls, inimigos ativos, alocações.
- Um cenário de benchmark (`--bench=40enemies`) que roda 60 s e imprime p50/p99
  de frame time — comparável entre commits.

## 4. Portões de qualidade por PR

Nenhum merge sem:

- [ ] `dotnet build -c ExportRelease` sem warning
- [ ] `dotnet test` verde
- [ ] `ContentValidator` passando
- [ ] nenhum nó 2D novo no gameplay (`grep` nos `.tscn` alterados)
- [ ] nenhum valor de balanceamento hardcoded em `.cs`
- [ ] eventos assinados têm desassinatura correspondente
- [ ] assets novos com `SOURCE.md` e licença compatível
- [ ] se tocou geometria da arena: navmesh rebakeada
- [ ] se mudou balanceamento: tabela da spec atualizada no mesmo commit

## 5. Bugs previsíveis — vigiar desde já

Esta lista existe porque são os erros que este desenho específico convida.

| Bug | Onde nasce | Prevenção |
|---|---|---|
| Handler vazado no pooling | inimigo reciclado sem desassinar | contrato de `ResetForSpawn` + teste de 100 ciclos |
| `Died` disparado duas vezes | dois golpes no mesmo frame | fila de dano + morte idempotente |
| Stat não volta ao base | forma revertida parcialmente | `RemoveBySource` + teste de igualdade exata |
| Hitbox órfã ativa | habilidade cancelada por morte | `End(cancelled: true)` sempre desliga a hitbox |
| Movimento dependente de framerate | `Lerp(a, b, delta)` | `SmoothDamp` / integração por delta correta |
| Input engolido | `IsActionJustPressed` lido em `_Process` | ler input em `_PhysicsProcess` |
| Menu nasce pausado | `GetTree().Paused` não limpo | `SceneRouter` despausa em toda transição |
| Combo do resolver ambíguo | duas habilidades com a mesma sequência | validação em tempo de carga que quebra o boot |
| Câmera girando com o player | câmera filha do player | `CameraRig` irmão, verificado em review |
| Stutter ao spawnar onda | instanciação em massa | `EnemyPool.Prewarm` |

## 6. Playtest

A partir do M5, toda semana:

1. Uma partida completa com cada personagem.
2. Anotar: onde morreu, qual habilidade nunca usou, o que não entendeu.
3. Métrica-chave: **o jogador executa habilidades de propósito?** Se as
   sequências só saem por acidente, o `TokenLifetime` ou o HUD estão errados.
4. Registrar em `docs/playtests/AAAA-MM-DD.md` — decisões de balanceamento
   precisam de rastro.

## 7. Definition of Done (por feature)

Uma feature está pronta quando:

1. compila sem warning;
2. tem testes na camada testável;
3. seus valores estão em `.tres`, não no código;
4. tem feedback visual e sonoro (uma mecânica sem feedback não existe para o
   jogador);
5. não regride o orçamento de frame;
6. a spec correspondente foi atualizada se o comportamento mudou;
7. foi jogada por alguém que não a implementou.
