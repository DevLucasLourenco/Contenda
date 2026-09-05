# 🤖 Guia: Sistema Multi-Agente Spec-Driven Puro

**Agentes leem a spec completa e decidem o que fazer. Zero foco pré-definido.**

---

## ⚡ Como usar

```bash
/implement-multiagent criar sistema de vida com barra de dano vermelha
```

Pronto! 4 agentes trabalham em paralelo, cada um claima as tarefas que faz sentido para ele.

---

## 🔄 Como funciona

### 1. **Architect** (cria lista de tarefas)
- Lê sua spec
- Identifica TODAS as tarefas necessárias
- **Não defina owner** — cada tarefa é genérica
- Cria `.claude/implement-tasks.json` com tarefas "unclaimed"

### 2. **Backend + Frontend + Tester** (trabalham em paralelo)
- Cada um **lê a spec completa**
- Cada um **lê a lista de tarefas**
- Cada um **decide qual tarefa faz sentido para ele**
  - Backend vê "criar componente Health" → "vou fazer isso"
  - Frontend vê "criar UI barra de vida" → "vou fazer isso"
  - Tester vê "testar Health" → "vou fazer isso"
- Cada um **claima sua tarefa** no JSON (marca `claimed_by`)
- Cada um **implementa, testa, commita**
- Cada um **marca como done**

**Ninguém interfere porque claima ANTES de implementar.**

### 3. **Integrate** (une tudo)
- Agrega commits
- Valida que ninguém tocou no mesmo arquivo

### 4. **Reviewer** (valida)
- Verifica compliance com CLAUDE.md
- Detecta bugs, regressões
- APPROVED ou CHANGES REQUESTED

### 5. **Report**
- Feature completa, testada, integrada

---

## 📋 Exemplo prático

### Você digita:
```bash
/implement-multiagent criar sistema de vida ao personagem com regeneração de 5%/s
```

### Architect cria:
```json
{
  "tasks": [
    {
      "id": "task-1",
      "description": "Criar componente Health com vida, maxVida, regeneração",
      "files": ["src/Components/Health.cs"],
      "status": "unclaimed",
      "claimed_by": null
    },
    {
      "id": "task-2",
      "description": "Criar Resource para balancear vida/regen do inimigo",
      "files": ["res/Data/Enemies/BasicEnemy.tres"],
      "status": "unclaimed",
      "claimed_by": null
    },
    {
      "id": "task-3",
      "description": "Criar UI barra de vida com animação de dano",
      "files": ["scenes/UI/HealthBar.tscn", "scenes/UI/HealthBar.cs"],
      "status": "unclaimed",
      "claimed_by": null
    },
    {
      "id": "task-4",
      "description": "Testes para regeneração, dano, morte",
      "files": ["tests/ComponentsTests/HealthTests.cs"],
      "status": "unclaimed",
      "claimed_by": null
    }
  ]
}
```

### Agentes claiam (simultaneamente):
```
Backend lê: "Vou fazer task-1 (Health) e task-2 (Resource)"
  → Edita JSON, marca como claimed
  → Implementa
  → Commit

Frontend lê: "Vou fazer task-3 (UI barra)"
  → Edita JSON, marca como claimed
  → Implementa
  → Commit

Tester lê: "Vou fazer task-4 (testes)"
  → Edita JSON, marca como claimed
  → Implementa
  → Commit
```

**Resultado: cada um fez sua parte, ninguém interferiu.**

---

## 🎯 Exemplos para seu jogo

```bash
# Sistema de combate
/implement-multiagent criar ataque básico com cooldown de 0.5s, animação de slash, efeito visual

# Inimigos
/implement-multiagent implementar inimigo básico que persegue player com IA simples e som de passos

# Persistência
/implement-multiagent adicionar save/load com 3 slots de jogo, preview de personagem em slot

# Poder especial
/implement-multiagent criar poder rajada de espada que dispara 3 ataques, custa 20 mana, cooldown 5s

# Efeitos
/implement-multiagent adicionar efeito de sangue ao acertar, partículas de impacto, som de hit
```

---

## 🤖 Como cada agente pensa

### Backend
Lê a spec: "criar sistema de vida com regeneração"
- Pensa: "Vou implementar o componente Health"
- Claima: task-1 (criar Health.cs)
- Implementa: propriedades, métodos, eventos
- Commita

### Frontend
Lê a spec: "criar sistema de vida com regeneração"
- Pensa: "Vou criar a UI barra de vida"
- Claima: task-3 (criar HealthBar.tscn)
- Implementa: cena, binding, animações
- Commita

### Tester
Lê a spec: "criar sistema de vida com regeneração"
- Pensa: "Vou testar o componente Health"
- Claima: task-4 (criar testes)
- Implementa: testes para dano, regeneração, morte
- Commita

**Ninguém foi pré-definido.** Cada um decidiu baseado na spec.

---

## ✅ Vantagens

✔️ **Verdadeiramente spec-driven** — nenhum agente tem "owner" pré-definido  
✔️ **Decisão independente** — cada um lê e decide  
✔️ **Zero conflito** — claima antes de implementar  
✔️ **Paralelo real** — trabalham simultaneamente  
✔️ **Flexível** — se uma tarefa não faz sentido para ninguém, fica unclaimed  

---

## 🚀 Dicas

- **Seja específico na spec**: "criar inimigo" é vago. "Criar inimigo basico que persegue player, tem 20 HP, spawna em grupo de 3, tem som de respiração" é claro.

- **Specs boas = arquitetura melhor**: Quanto mais claro, melhor o architect divide.

- **Componentes reutilizáveis**: Se a tarefa é reutilizável, mais agentes podem fazer.

- **Dados em Resources**: Balanceamento **sempre** em `.tres`, nunca hardcoded.

- **Testes desde o início**: Frontend testa UI, Backend testa lógica, Tester testa tudo junto.

---

## 📂 Arquivo de coordenação

`.claude/implement-tasks.json` é criado/atualizado automaticamente:

```json
{
  "tasks": [
    {
      "id": "task-1",
      "description": "...",
      "claimed_by": null → "backend" → (done)
      "status": "unclaimed" → "claimed" → "done"
    }
  ]
}
```

Agentes **editam esse arquivo** para claimar/marcar tarefas.

---

## 🎮 Teste agora

```bash
/implement-multiagent criar poder especial "golpe giratório" que faz 2x dano, custa 15 mana, cooldown 3s
```

Veja 4 agentes lendo a spec, decidindo independentemente, implementando em paralelo! 🚀

---

## 📚 Leia também

- [CLAUDE.md](CLAUDE.md) — regras do projeto
- [Visão geral](docs/00-visao-geral.md)
- [Arquitetura técnica](docs/specs/01-arquitetura-tecnica.md)
