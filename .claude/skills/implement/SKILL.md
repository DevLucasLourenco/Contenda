---
name: implement-multiagent
description: Spec-driven puro. Agentes leem spec, claiam tarefas, trabalham independentemente. Zero conflito.
disable-model-invocation: true
---

# Spec-Driven Multi-Agent Orchestrator (Pure)

Você é o **IMPLEMENTATION ORCHESTRATOR**.

Agentes **leem a spec completa**, **decidem o que fazer**, **claiam tarefas no JSON**.

---

## MODELO

**Verdadeiramente spec-driven**:

1. **Architect** — lê spec, lista tarefas (sem owner pré-definido)
2. **Todos agentes** — leem spec + tasks, claiam o que faz sentido
3. **Paralelo**: Agentes trabalham independentemente, sem coordenação pré-definida
4. **Locking automático**: JSON evita que dois claiem a mesma tarefa
5. **Reviewer** — valida tudo
6. **Integrate** — coleta commits, resolve conflitos, valida

---

## FASE 0 — SAFETY CHECK

1. Leia `CLAUDE.md`.
2. `git status` — identifique alterações não commitadas.
3. Preserve trabalho existente.
4. Descubra arquitetura.

Se houver `.claude/implement-tasks.json` existente, **não sobrescreva** — continue de lá.

---

## FASE 1 — ARCHITECT (Delegado)

Descreva a task:

> Você é o Architect. Leia a spec abaixo e:
>
> 1. Analise a arquitetura existente (leia CLAUDE.md, projete existente)
> 2. Identifique TODAS as tarefas necessárias
> 3. **Não defina owner** — tarefas são genéricas
> 4. Crie/atualize `.claude/implement-tasks.json` com:
>    - `claimed_by: null`
>    - `status: "unclaimed"`
>    - Descrição clara
>    - Arquivos afetados
>    - Critérios de aceitação
>    - Dependências
>
> Retorne: Análise arquitetônica + lista de tarefas criadas.

Aguarde resultado. Valide que tarefas são claras e independentes.

---

## FASE 2 — PARALLEL CLAIM & IMPLEMENT

Descreva para TODOS os agentes (backend, frontend, tester) simultaneamente:

> Vocês estão vendo a feature: `$ARGUMENTS`
>
> 1. Leia `.claude/implement-tasks.json`
> 2. Leia a spec completa
> 3. **Identifique qual dessas tarefas VOCÊ pode fazer** — baseado no seu tipo
> 4. Para cada tarefa que você quer fazer:
>    - Edite `.claude/implement-tasks.json`: `claimed_by: "<seu-tipo>"`, `status: "claimed"`
>    - Faça commit disso PRIMEIRO (reserva a tarefa)
>    - Implemente
>    - Teste
>    - Commit final
>    - Marque como `status: "done"`
> 5. Retorne: tarefas claimas + commits

**Não espere sequencialmente.** Todos começam simultaneamente.

Cada agente vai caiimar diferentes tarefas baseado na spec.

Se houver conflito de tarefa (dois tentam claimer a mesma), o segundo commit vai falhar no JSON — agente detecta e claima outra.

---

## FASE 3 — COLLECT & CONFLICT CHECK

Quando todos retornarem:

1. Verifique `.claude/implement-tasks.json`:
   - Todas tarefas têm `claimed_by` preenchido?
   - Todas têm `status: "done"`?
2. Colete commits de cada agente.
3. **Valide conflitos de arquivo**:
   - Se dois commits tocam o mesmo arquivo → **ERRO**
   - Cause: Architect dividiu mal as tarefas
   - Solution: Peça replanejamento ou resolução manual
4. Ordene commits por dependência (tasks que dependem de outras vêm depois).

---

## FASE 4 — INTEGRATE

Integre commits em ordem:

```bash
# Para cada commit, em ordem de dependência:
git cherry-pick <commit-hash>
```

Antes de cada cherry-pick: `git status`
Após cada cherry-pick: `git status` + `git diff HEAD~1..HEAD`

Se conflito: resolva manualmente, preferindo solução que preserve contratos.

---

## FASE 5 — VALIDATION

```bash
dotnet build Contenda.sln -c ExportDebug
[execute testes]
```

Se falhar: identifique commit culpado. Comunique ao agente relevante.

---

## FASE 6 — REVIEW (Delegado)

Descreva a task:

> Você é o Reviewer. 
>
> Spec: `$ARGUMENTS`
> 
> Tasks completadas em `.claude/implement-tasks.json`
>
> Analise:
> - Compliance com CLAUDE.md
> - Bugs, regressões
> - Testes adequados
> - Escopo respeitado
>
> Retorne: APPROVED ou CHANGES REQUESTED

Se CHANGES REQUESTED:
- Agentes fazem novos commits
- Integra de novo
- Reviewer valida novamente

Se APPROVED: continue.

---

## FASE 7 — FINAL VALIDATION

```bash
git status
git diff
git log --oneline -n 20
dotnet build Contenda.sln -c ExportDebug
[execute testes]
```

Certifique:
- Sem alterações inesperadas
- Sem secrets
- Sem logs de debug
- Critérios de aceitação atendidos

---

## FASE 8 — REPORT

```
## ✅ Implementado

[Resumo da feature]

## 📊 Tarefas

[Lista com claimed_by + status]

## 🤖 Agentes Participantes

- architect ✅
- backend ✅ (tarefas: X, Y)
- frontend ✅ (tarefas: Z)
- tester ✅ (tarefas: A)
- reviewer ✅

## 🔍 Review

APPROVED

## 📦 Commits

[Lista de commits com hashes]

## ⚠️ Anotações

[Observações]
```

---

## TASK-TRACKING JSON FORMAT

```json
{
  "spec": "Adicionar sistema de vida ao personagem com barra de dano",
  "created_at": "2026-09-04T21:30:00Z",
  "tasks": [
    {
      "id": "task-1",
      "description": "Criar componente Health com vida/maxVida",
      "files": ["src/Components/Health.cs"],
      "acceptance_criteria": [
        "Componente tem vida/maxVida",
        "Método Damage(amount)",
        "Evento OnDeath"
      ],
      "depends_on": [],
      "claimed_by": null,
      "status": "unclaimed"
    },
    {
      "id": "task-2",
      "description": "Criar UI barra de vida que mostra dano em tempo real",
      "files": ["scenes/UI/HealthBar.tscn"],
      "acceptance_criteria": [
        "Barra vermelha visível",
        "Atualiza ao receber Damage"
      ],
      "depends_on": ["task-1"],
      "claimed_by": null,
      "status": "unclaimed"
    }
  ],
  "risks": ["Integração UI <-> Health pode ter timing"],
  "architecture_notes": "Health é compartilhado (Player e Enemy)"
}
```

Depois que agentes claiam:

```json
{
  "tasks": [
    {
      "id": "task-1",
      "claimed_by": "backend",
      "status": "claimed"  // depois "done"
    },
    {
      "id": "task-2",
      "claimed_by": "frontend",
      "status": "claimed"  // depois "done"
    }
  ]
}
```

---

## WORKFLOW VISUAL

```
Usuario: /implement-multiagent <spec>
    |
    v
Architect → .claude/implement-tasks.json
            (tasks: unclaimed)
    |
    +────────────────────┬────────────────────┬────────────────┐
    |                    |                    |                |
    v                    v                    v                v
Backend              Frontend            Tester           (ninguém?)
(lê spec)            (lê spec)            (lê spec)
(claima task-1)      (claima task-2)      (claima task-3)
(implementa)         (implementa)         (implementa)
(commit)             (commit)             (commit)
    |                    |                    |
    └────────────────────┼────────────────────┘
                         |
                    INTEGRATE
                         |
                      VALIDATE
                         |
                      REVIEWER
                         |
                      REPORT
```

**Agentes trabalham independentemente. Nenhum foco pré-definido. Spec-driven puro.**

