# Riscos e decisões de arquitetura

## Parte 1 — ADRs

Decisões tomadas na concepção, registradas com a alternativa que foi descartada.
Revisitar uma delas exige uma nova entrada, não uma edição silenciosa.

---

### ADR-001 — Godot 4.7.x .NET com C#, sem GDScript

**Decisão:** toda a lógica em C#, no editor .NET do Godot.

**Alternativas:** GDScript (mais integrado, mais rápido de prototipar); Unity ou
Unreal (mais maduros para 3D de ação).

**Consequências:** ✅ tipagem forte, ferramentas .NET, testes com xUnit fora da
engine, refatoração segura. ❌ **sem exportação Web**; ciclo de compilação mais
lento; menos exemplos da comunidade em C#.

**Status:** aceito — requisito explícito do projeto.

---

### ADR-002 — 3D real com câmera travada, não 2.5D por sprites

**Decisão:** simulação, colisão, navegação e render 3D. O "2.5D" é apresentação.

**Alternativa:** sprites 2D em um plano com profundidade falsa.

**Consequências:** ✅ profundidade real, altura utilizável, navegação 3D, e
**nenhuma justificativa arquitetural para o projeto derivar para 2D** — que foi
o problema das versões anteriores. ❌ mais custo de render e de assets.

**Status:** aceito. É o pilar não negociável do projeto.

---

### ADR-003 — Câmera perspectiva com FOV baixo, não ortográfica

**Decisão:** perspectiva, FOV 38°, distância 14 m.

**Alternativa:** ortográfica (visual isométrico clássico).

**Consequências:** ✅ profundidade preservada; altura e paralaxe legíveis.
❌ escala varia com a distância à câmera; enquadramento exige mais cuidado.

**Status:** aceito. Ortográfica fica como experimento pós-MVP
(`CameraSettings.UseOrthogonal`).

---

### ADR-004 — Composição por componentes, não herança

**Decisão:** `CharacterController` é um contêiner; a lógica vive em componentes
`Node`; a diferença entre personagens é dado.

**Alternativa:** hierarquia `BaseCharacter → Swordsman/Gunslinger`.

**Consequências:** ✅ jogador e inimigo compartilham código; adicionar
personagem não toca no core; testável. ❌ mais arquivos; comunicação entre
componentes precisa de disciplina (o `CharacterContext`).

**Status:** aceito.

---

### ADR-005 — Nenhum projeto de terceiro como dependência

**Decisão:** estudar Isometric 3D Toolkit, Godot Top-Down Template,
GDAbilitySystem, Godot4-3D-Characters e RoboBlast; **reescrever** em C#.

**Alternativa:** usar GDAbilitySystem e o Isometric Toolkit como fundação.

**Consequências:** ✅ sem dependência de API instável (o autor do Isometric
Toolkit avisa que ela muda muito); sem misturar GDScript; sem herdar licença
CC BY. ❌ mais código próprio.

**Status:** aceito. Se o sistema de habilidades crescer muito (dezenas de
buffs/debuffs/status), reavaliar a adoção do GDAbilitySystem (MIT).

---

### ADR-006 — `StatBlock` central com modificadores por fonte

**Decisão:** transformações e buffs escrevem modificadores; componentes leem o
valor final; reversão é `RemoveBySource`.

**Alternativa:** transformação altera diretamente os campos dos componentes.

**Consequências:** ✅ reversão trivial e correta; buffs empilháveis de graça.
❌ uma indireção a mais em toda leitura de stat (mitigada por cache).

**Status:** aceito. Sem isso, "reverter transformação" seria uma fonte
permanente de bugs.

---

### ADR-007 — Scroll seleciona, M3 ativa

**Decisão:** separar seleção de ativação.

**Alternativa:** scroll ativa diretamente (leitura literal do requisito).

**Consequências:** ✅ sem ativação acidental; permite planejar a forma antes de
gastar mana; escala para 10 formas. ❌ uma tecla a mais para aprender.

**Status:** aceito — resolve uma ambiguidade real da especificação original.

---

### ADR-008 — M2 confirma o match exato da sequência atual

**Decisão:** se o nó atual da trie tem uma habilidade, M2 a dispara, mesmo que
existam sequências mais longas com esse prefixo.

**Alternativa:** aguardar uma janela para ver se o jogador continua digitando.

**Consequências:** ✅ input determinístico, sem latência. ❌ restrição de
design: evitar sequências longas cujo prefixo já seja habilidade do mesmo
personagem.

**Status:** aceito. Validado em tempo de carga.

---

### ADR-009 — Pooling de inimigos desde o M5

**Decisão:** `EnemyPool` com prewarm, não `Instantiate`/`QueueFree`.

**Alternativa:** instanciar sob demanda e otimizar depois.

**Consequências:** ✅ sem GC spike nem stutter com dezenas de inimigos.
❌ contrato de reciclagem rigoroso; risco de estado vazado se descuidar.

**Status:** aceito. O contrato de `ResetForSpawn` e o teste de 100 ciclos são a
contrapartida obrigatória.

---

### ADR-010 — Arte por último (M8), depois do MVP jogável

**Decisão:** cápsulas e primitivas até o M7; assets CC0 no M8.

**Alternativa:** arte desde o início, para motivação.

**Consequências:** ✅ mecânica validada antes do investimento caro; trocar
modelo é uma linha de `.tres`. ❌ o projeto fica visualmente feio por semanas.

**Status:** aceito.

---

### ADR-011 — Personagem encara o cursor do mouse

**Decisão:** `FaceMode.Aim` como padrão.

**Alternativa:** encarar a direção do movimento.

**Consequências:** ✅ o alcance do Gunslinger vira vantagem real; mira precisa
com câmera fixa. ❌ mais carga cognitiva (mãos fazendo coisas diferentes).

**Status:** aceito, com `FaceMode.Movement` disponível para comparação no M1.

---

### ADR-012 — O jogo se chama Contenda

**Decisão:** o nome do projeto, do executável, do assembly e da raiz de namespace
é **Contenda**.

**Alternativa:** *Infinity Wars*, o codinome usado na conversa de concepção e nas
tentativas anteriores do projeto.

**Consequências:** ✅ nome já refletido em `project.godot`, `Contenda.csproj`,
`Contenda.sln`, `RootNamespace` e nos caminhos `user://`; nada a migrar.
❌ os ZIPs antigos de *Infinity Wars* em `Downloads` não são mais o mesmo
produto — tratá-los como referência histórica, não como base.

**Status:** aceito, decidido pelo usuário. Fecha o risco R14.

---

## Parte 2 — Riscos do projeto

### Riscos altos

| # | Risco | Impacto | Prob. | Mitigação | Sinal de alerta |
|---|---|---|---|---|---|
| R1 | **O sistema de comandos não é divertido** | reprojeto da mecânica central | média | go/no-go no M3 com plano B (teclas 1–4 + sequências como bônus) | playtester não executa habilidade de propósito em 2 min |
| R2 | **Performance com 40 inimigos** | redesenho do modo horda | média | pooling, repath escalonado e LOD desde o M5; orçamento medido | frame time p99 > 16.6 ms no M5 |
| R3 | **Escopo crescendo** ("só mais um personagem") | MVP nunca fecha | **alta** | escopo congelado na visão geral; tudo novo vai para o backlog | discussão sobre modo online antes do M7 |
| R4 | **Sequências disparando ao andar** | mecânica frustrante | média | intervalo mínimo entre tokens iguais; tuning de `TokenLifetime` no M3 | habilidade saindo sem intenção em playtest |

### Riscos médios

| # | Risco | Mitigação |
|---|---|---|
| R5 | Retarget do Mixamo deformando rigs | validar **um** personagem completo antes de processar todos (M8) |
| R6 | Estado vazado no pooling | contrato de `ResetForSpawn` + teste de 100 ciclos |
| R7 | Curva de aprendizado do C# no Godot | estimativas do roadmap já incluem; M0 e M1 servem de aquecimento |
| R8 | Balanceamento infinito no M9 | timebox de 3 dias; publicar 0.1 e iterar com feedback real |
| R9 | Assets de licença incompatível entrando | `SOURCE.md` obrigatório + auditoria por grep no CI |
| R10 | Camadas de física mal configuradas no editor | checagem de time em código, além das camadas |

### Riscos baixos, mas registrados

| # | Risco | Nota |
|---|---|---|
| R11 | Sem export Web com C# | conhecido e aceito; se Web virar requisito, é decisão de reescrita — não de ajuste |
| R12 | macOS exigindo notarização | fora do escopo da 0.1 |
| R13 | Godot 4.7 mudando API em minor | fixar a versão do editor e do `GodotSharp` no `.csproj` |
| ~~R14~~ | ~~Nome "Contenda" vs "Infinity Wars"~~ | **fechado** — ver ADR-012 |

### Riscos já mitigados pelo desenho

| Risco histórico | Como o desenho elimina |
|---|---|
| **O jogo virar 2D** | proibição de nós 2D, verificação no CI, `CharacterBody3D` em tudo (ADR-002) |
| `Player.cs` de 4.000 linhas | composição por componentes (ADR-004) |
| `if (personagem == swordsman)` espalhado | diferença é 100% dado (`.tres`) |
| `SwordPlayer.cs` / `GunPlayer.cs` duplicados | uma cena, um controller, dados diferentes |
| Transformação deixando stat quebrado | `RemoveBySource` (ADR-006) |

## Parte 3 — Revisão

Reavaliar esta página ao fim de cada milestone: marcar riscos materializados,
fechar os superados, adicionar os que aparecerem. Um risco que ninguém releu não
está sendo gerenciado.
