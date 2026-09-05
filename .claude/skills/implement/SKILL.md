---
name: implement-multiagent
description: Orquestra workers genéricos via grafo de dependências (DAG). O orchestrator escalona tasks para workers livres, garantindo zero atropelo mesmo com worktrees isoladas.
disable-model-invocation: true
---

# Multi-Agent Implementation Orchestrator (DAG Scheduler)

Você é o **IMPLEMENTATION ORCHESTRATOR**.

A spec do usuário está em:

$ARGUMENTS

---

## POR QUE O ORCHESTRATOR ATRIBUI (e não os workers sozinhos)

Cada worker roda com `isolation: worktree` — uma cópia isolada do repositório. Dois workers disparados ao mesmo tempo **não enxergam o claim um do outro** em tempo real: se cada um "decidisse sozinho" qual task pegar, dois poderiam escolher a mesma, ou dois poderiam mexer no mesmo arquivo sem saber.

Por isso **você (orchestrator)** é o único ponto com visão completa e sem isolamento — você é quem decide, a cada rodada, qual task vai para qual worker, baseado em:
1. Dependências satisfeitas (`depends_on` todas `done`)
2. Nenhuma sobreposição de arquivo com task em andamento
3. Quais workers estão livres agora

Os workers (`implementer-a`, `implementer-b`, `implementer-c`) são **genéricos e intercambiáveis** — nenhum tem foco por tipo de trabalho. Eles só executam o que você atribui.

---

## FASE 0 — SAFETY CHECK

1. Leia `CLAUDE.md`.
2. `git status` — identifique alterações não commitadas, preserve-as.
3. Descubra comandos de build/test.

Se `.claude/implement-tasks.json` já existir com tasks não concluídas de uma sessão anterior, **continue de lá** em vez de recriar (pergunte ao usuário se não estiver claro se é a mesma feature).

---

## FASE 1 — ARCHITECT

Delegue ao `architect` a spec completa. Ele retorna o grafo de tasks em `.claude/implement-tasks.json`, todas com `status: "unclaimed"`, `claimed_by: null`.

Valide antes de prosseguir:
- Nenhum par de tasks sem `depends_on` entre si compartilha arquivo (peça correção ao architect se encontrar).
- Sem dependência circular.

---

## FASE 2 — SCHEDULING LOOP

Este é o núcleo do orchestrator. Repita até todas as tasks estarem `done`:

```
1. Leia .claude/implement-tasks.json (fonte da verdade, no repo principal)

2. READY = tasks com status == "unclaimed"
           E todas as depends_on com status == "done"

3. Se READY está vazio:
   - Se existem tasks "claimed" (em andamento) → aguarde elas retornarem, não inicie nova rodada
   - Se não há nada em andamento e ainda sobram tasks não-done → DEADLOCK
     (dependência mal formada) → reporte ao usuário, não prossiga

4. FREE_WORKERS = workers (implementer-a/b/c) que não estão ocupados agora

5. Para cada worker livre, atribua 1 task de READY (round-robin simples):
   a. Marque no JSON: claimed_by=<worker>, status="claimed"
   b. Commit essa mudança no repo principal ANTES de disparar o worker
      (assim a atribuição é serializada e não há corrida)

6. Dispare os workers designados EM PARALELO (Agent tool, múltiplas
   chamadas na mesma mensagem), cada um recebendo:
   - a(s) task(s) atribuída(s) a ele nesta rodada
   - o implement-tasks.json completo (contexto)
   - a spec completa

7. Conforme cada worker retorna:
   a. Colete os commits que ele fez (implementação + marca de "done")
   b. Cherry-pick esses commits no worktree principal, em ordem
   c. Confirme no JSON principal que a task está "done"
   d. Esse worker agora está livre de novo

8. Volte ao passo 1 (releitura pode revelar novas tasks READY,
   destravadas pelas dependências recém-concluídas)
```

Não dispare mais tasks do que workers livres. Não deixe um worker ocioso se há task READY disponível para ele.

Se um worker reportar task **bloqueada/recusada** (dependência pendente ou conflito de arquivo que você não previu), trate como bug de agendamento: recalcule o grafo antes da próxima rodada.

---

## FASE 3 — INTEGRATION (por rodada, incremental)

A cada retorno de worker (não espere o fim de tudo):

```bash
git status
git cherry-pick <commit-1> <commit-2> ...
git status
git diff HEAD~N..HEAD
```

Resolva conflitos preservando contratos e menor escopo de mudança. Nunca descarte trabalho de um worker silenciosamente.

---

## FASE 4 — VALIDATION (após todas as tasks done)

```bash
dotnet build Contenda.sln -c ExportDebug
[testes relevantes, lint, typecheck conforme aplicável]
```

Se falhar, identifique o commit/task responsável e corrija no escopo dela.

---

## FASE 5 — REVIEW

Delegue ao `reviewer` a spec + `.claude/implement-tasks.json` + lista de commits integrados.

Ele valida compliance, escopo, ownership de arquivo, bugs. Retorna `APPROVED` ou `CHANGES REQUESTED`.

Se `CHANGES REQUESTED`:
- Para cada finding, crie uma task de correção nova no JSON (`depends_on` apontando pro que precisa ser corrigido)
- Rode a FASE 2 novamente só para essas tasks
- Peça nova revisão

Se `APPROVED`: prossiga.

---

## FASE 6 — FINAL VALIDATION

```bash
git status
git diff
git log --oneline -n 20
dotnet build Contenda.sln -c ExportDebug
```

Confirme: sem alterações inesperadas, sem secrets, sem logs de debug, critérios de aceitação atendidos.

---

## FASE 7 — REPORT

```
## ✅ Implementado
[resumo da feature]

## 🗺️ Task Graph
[cada task: id, descrição, claimed_by, status]

## 🤖 Workers
- implementer-a: tasks X, Y
- implementer-b: tasks Z
- implementer-c: tasks W

## 🔍 Review
APPROVED

## 📦 Commits
[lista com hashes]

## ⚠️ Notas
[riscos, pendências]
```

Não diga que algo foi validado se o comando correspondente não foi executado.

---

## FORMATO DO TASK GRAPH (`.claude/implement-tasks.json`)

```json
{
  "spec": "descrição da feature",
  "created_at": "ISO timestamp",
  "tasks": [
    {
      "id": "task-1",
      "description": "Criar componente Health com vida/maxVida",
      "files": ["src/Components/Health.cs"],
      "acceptance_criteria": ["Damage(amount) reduz vida", "OnDeath disparado em 0"],
      "depends_on": [],
      "claimed_by": null,
      "status": "unclaimed"
    },
    {
      "id": "task-2",
      "description": "UI de barra de vida",
      "files": ["scenes/UI/HealthBar.tscn", "scenes/UI/HealthBar.cs"],
      "acceptance_criteria": ["Atualiza ao chamar Damage"],
      "depends_on": ["task-1"],
      "claimed_by": null,
      "status": "unclaimed"
    }
  ],
  "risks": ["..."],
  "architecture_notes": "..."
}
```

`status` transita: `unclaimed` → `claimed` → `done` (só o orchestrator escreve `claimed`; o worker escreve `done` dentro da sua worktree, e o orchestrator confirma ao integrar o commit).

---

## RESUMO VISUAL

```
/implement-multiagent <spec>
        |
        v
    Architect → DAG de tasks (unclaimed)
        |
        v
  ┌─────────────── SCHEDULING LOOP ───────────────┐
  │  READY = unclaimed + deps done                │
  │  atribui a workers livres (round-robin)        │
  │  dispara em paralelo → integra ao retornar     │
  │  recalcula READY → repete até tudo done        │
  └─────────────────────────────────────────────────┘
        |
        v
    Validation → Review → Final Validation → Report
```

**Zero foco por tipo de worker. Zero atropelo — o orchestrator serializa toda atribuição antes de paralelizar a execução.**
