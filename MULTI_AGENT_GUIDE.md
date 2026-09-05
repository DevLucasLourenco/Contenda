# 🤖 Guia: Sistema Multi-Agente com Scheduler de Dependências

**Workers genéricos, sem foco por tipo. O orchestrator monta o grafo de tasks e atribui — garantindo zero atropelo.**

---

## ⚡ Como usar

```bash
/implement-multiagent criar sistema de vida com barra de dano vermelha
```

---

## 🔄 Como funciona

### 1. **Architect** — monta o grafo de tasks (DAG)
- Lê a spec
- Quebra em tarefas atômicas
- Define `depends_on` entre elas
- **Garante que duas tasks sem dependência nunca compartilham arquivo**
- Cria `.claude/implement-tasks.json`, tudo `unclaimed`

### 2. **Orchestrator** — escalona (o coração do sistema)

Por que o orchestrator decide, e não os workers sozinhos? Porque cada worker roda numa **worktree isolada** — uma cópia separada do repositório. Se dois workers "escolhessem" tarefas por conta própria ao mesmo tempo, **não veriam a escolha um do outro** e poderiam colidir.

Então o orchestrator:
1. Calcula quais tasks estão **prontas** (dependências satisfeitas)
2. Vê quais workers estão **livres**
3. **Atribui** 1 task por worker livre, grava isso no JSON principal
4. Só então dispara os workers em paralelo
5. Ao cada um voltar, integra o commit e libera o worker para a próxima task pronta

Isso se repete até o grafo inteiro estar `done`.

### 3. **Workers** (`implementer-a`, `implementer-b`, `implementer-c`) — genéricos
- Não têm foco fixo — o mesmo worker pode implementar lógica C#, UI, ou testes, dependendo do que for atribuído
- Recebem a(s) task(s) já decidida(s) para eles
- Antes de mexer em qualquer arquivo: conferem que a dependência está `done` e que ninguém mais está mexendo naquele arquivo
- Implementam, testam, commitam, marcam `done`

### 4. **Reviewer** — valida no final
- Compliance com `CLAUDE.md`
- Nenhuma sobreposição de arquivo indevida
- Bugs, regressões, escopo

---

## 📋 Exemplo prático

### Você digita:
```bash
/implement-multiagent criar sistema de vida com regeneração de 5%/s
```

### Architect cria o DAG:
```json
{
  "tasks": [
    { "id": "task-1", "description": "Componente Health (vida/regen)",
      "files": ["src/Components/Health.cs"], "depends_on": [], "status": "unclaimed" },

    { "id": "task-2", "description": "Resource de balanceamento de vida",
      "files": ["res/Data/Enemies/BasicEnemy.tres"], "depends_on": ["task-1"], "status": "unclaimed" },

    { "id": "task-3", "description": "UI barra de vida",
      "files": ["scenes/UI/HealthBar.tscn"], "depends_on": ["task-1"], "status": "unclaimed" },

    { "id": "task-4", "description": "Testes de Health",
      "files": ["tests/ComponentsTests/HealthTests.cs"], "depends_on": ["task-1"], "status": "unclaimed" }
  ]
}
```

### Orchestrator escalona:

**Rodada 1** — só `task-1` está pronta (as outras dependem dela):
```
implementer-a ← task-1 (única pronta)
implementer-b ← livre, aguardando
implementer-c ← livre, aguardando
```

**Rodada 2** — assim que `task-1` fica `done`, `task-2`, `task-3`, `task-4` ficam prontas simultaneamente:
```
implementer-a ← task-2
implementer-b ← task-3
implementer-c ← task-4
```

Todos em paralelo, sem colisão — porque o orchestrator já garantiu que `task-2`, `task-3`, `task-4` não compartilham arquivo entre si.

---

## 🎯 Exemplos para seu jogo

```bash
/implement-multiagent criar ataque básico com cooldown de 0.5s, animação de slash, efeito visual

/implement-multiagent implementar inimigo básico que persegue player com IA simples e som de passos

/implement-multiagent adicionar save/load com 3 slots de jogo

/implement-multiagent criar poder rajada de espada: 3 ataques, 20 mana, cooldown 5s

/implement-multiagent adicionar efeito de sangue ao acertar, partículas de impacto
```

---

## ✅ Por que isso evita atropelo de verdade

| Risco | Como é evitado |
|---|---|
| Dois workers pegam a mesma task | Orchestrator atribui e commita ANTES de disparar — nunca dois recebem a mesma |
| Dois workers editam o mesmo arquivo | Architect garante que tasks sem dependência nunca compartilham `files` |
| Worker implementa fora de ordem | Worker confere `depends_on` antes de agir; se não está `done`, para e reporta |
| Trabalho duplicado | Cada task só é atribuída uma vez; workers não "escolhem" tasks soltas |

---

## 🚀 Dicas

- **Seja específico na spec** — quanto mais claro, melhor o architect monta o grafo.
- **Tasks pequenas = mais paralelismo** — o architect deve preferir granularidade fina.
- **Dados em Resources** — balanceamento sempre em `.tres`, nunca hardcoded.

---

## 📂 Arquivo de coordenação

`.claude/implement-tasks.json` — fonte da verdade do grafo, mantida no repositório principal (fora das worktrees isoladas) pelo orchestrator.

```json
{
  "tasks": [
    { "id": "task-1", "claimed_by": "implementer-a", "status": "done" }
  ]
}
```

`status`: `unclaimed` → `claimed` (orchestrator atribuiu) → `done` (worker terminou e foi integrado).

---

## 🎮 Teste agora

```bash
/implement-multiagent criar poder especial "golpe giratório": 2x dano, 15 mana, cooldown 3s
```

---

## 📚 Leia também

- [CLAUDE.md](CLAUDE.md) — regras do projeto
- [Visão geral](docs/00-visao-geral.md)
- [Arquitetura técnica](docs/specs/01-arquitetura-tecnica.md)
