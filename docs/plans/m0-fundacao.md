# M0 — Fundação técnica

**Objetivo:** um projeto Godot 4.7.x .NET que compila, roda, tem CI verde e
convenções escritas. Nenhuma mecânica ainda.

**Esforço:** 2–3 dias · **Depende de:** nada

## Entregáveis

- Projeto Godot .NET com C# compilando pelo editor e pela linha de comando
- Estrutura de pastas da [spec 01](../specs/01-arquitetura-tecnica.md)
- `InputMap` completo com todas as ações da [spec 03](../specs/03-input-comandos-e-combos.md)
- Autoloads vazios mas registrados
- Camadas de física nomeadas
- Projeto de testes xUnit com 1 teste passando
- CI no GitHub Actions
- `.gitignore`, `.gitattributes` (LFS), `LICENSE`, `THIRD-PARTY-NOTICES.md`

## Tarefas

### 1. Ambiente

- [ ] Instalar **Godot 4.7.x — .NET** e **.NET SDK 8**
- [ ] `dotnet --version` e o editor abrindo um projeto C# de teste
- [ ] Configurar o editor externo (VS Code / Rider) em Editor Settings → Dotnet

### 2. Criar o projeto

- [ ] `git init` no diretório (hoje ele não é um repositório)
- [ ] Novo projeto Godot na raiz, renderer **Forward+**
- [ ] Project → Tools → **C# → Create C# solution**
- [ ] `project.godot`: nome `Contenda`, versão `0.1.0`, resolução base
      1920×1080, modo de esticamento `canvas_items` / `expand`
- [ ] `.csproj`: `<TargetFramework>net8.0`, `<Nullable>enable`,
      `<TreatWarningsAsErrors>true`, `<LangVersion>latest`

> **Duas armadilhas confirmadas na implementação (SDK .NET 10.0.202):**
>
> 1. As configurações do `Godot.NET.Sdk` são **`Debug`, `ExportDebug` e
>    `ExportRelease`** — não existe `Release`. Um `.sln` gerado por
>    `dotnet new sln` mapeia `Release|Any CPU` para `Debug`, então
>    `dotnet build -c Release` **compila Debug em silêncio**. O `.sln` deste
>    repositório declara as três configurações corretas; com isso, um `-c Release`
>    equivocado falha alto em vez de mentir.
> 2. `dotnet new sln` no SDK 10 gera **`.slnx`** (formato novo) por padrão. O
>    editor Godot e as imagens de CI esperam `.sln` clássico — use
>    `dotnet new sln --format sln`.

### 3. Estrutura de pastas

- [ ] Criar `src/`, `scenes/`, `data/`, `assets/`, `tests/`, `tools/` com a
      subdivisão da [spec 01](../specs/01-arquitetura-tecnica.md) §3
- [ ] `.gdignore` em `docs/` e `tools/`
- [ ] Um `.gitkeep` em cada pasta vazia

### 4. Camadas de física

- [ ] Nomear as 10 camadas em Project Settings → Layer Names → 3D Physics
      exatamente como na [spec 01](../specs/01-arquitetura-tecnica.md) §8
- [ ] `src/Core/GameConstants.cs` com as constantes espelhando os bits:

```csharp
public static class PhysicsLayers
{
    public const uint World         = 1 << 0;
    public const uint PlayerBody    = 1 << 1;
    public const uint EnemyBody     = 1 << 2;
    public const uint PlayerHitbox  = 1 << 3;
    public const uint EnemyHitbox   = 1 << 4;
    public const uint PlayerHurtbox = 1 << 5;
    public const uint EnemyHurtbox  = 1 << 6;
    public const uint Projectile    = 1 << 7;
    public const uint GroundPlane   = 1 << 8;
    public const uint Interactable  = 1 << 9;
}
```

### 5. InputMap

- [ ] Registrar `move_up/down/left/right`, `attack_basic`, `command_confirm`,
      `form_prev`, `form_next`, `form_activate`, `pause`, `dodge`
- [ ] `GameConstants.InputActions` com `StringName` estático para cada uma
- [ ] Deadzone 0.2 nos eixos

### 6. Autoloads

- [ ] `GameBootstrap`, `GameSession`, `SceneRouter`, `GameEvents`,
      `AudioDirector` — classes vazias com `_Ready` logando o nome
- [ ] Registrar em Project Settings → Autoload, nessa ordem
- [ ] `ServiceLocator` com acesso tipado (`ServiceLocator.Session`)

### 7. Testes

- [ ] `tests/Contenda.Tests/Contenda.Tests.csproj` (xUnit, net8.0)
- [ ] Referenciar o projeto principal
- [ ] Um teste trivial sobre `GameConstants` para validar o encanamento
- [ ] Adicionar ambos os projetos ao `Contenda.sln`

> O projeto de testes **não** referencia `GodotSharp` para lógica pura. Se um
> teste precisar de tipos do Godot (`Vector3`), a referência ao pacote
> `GodotSharp` do NuGet resolve, sem abrir a engine.

### 8. Git

- [ ] `.gitignore`:

```
.godot/
.mono/
bin/
obj/
*.user
export_presets.cfg
```

- [ ] `.gitattributes` com LFS:

```
*.glb  filter=lfs diff=lfs merge=lfs -text
*.png  filter=lfs diff=lfs merge=lfs -text
*.jpg  filter=lfs diff=lfs merge=lfs -text
*.ogg  filter=lfs diff=lfs merge=lfs -text
*.wav  filter=lfs diff=lfs merge=lfs -text
*.tres text
*.tscn text
```

- [ ] `git lfs install`
- [ ] `LICENSE` e `THIRD-PARTY-NOTICES.md` (esqueleto)
- [ ] Commit inicial; branch `main` protegido, trabalho em `feature/*`

### 9. CI

- [ ] `.github/workflows/ci.yml`: setup-dotnet → restore →
      `dotnet build -c ExportRelease` → `dotnet test`
- [ ] Validar que warning quebra o build
- [ ] Job separado (pode ficar `continue-on-error` no M0) com Godot headless
      fazendo `--import`

### 10. Convenções

- [ ] Escrever [`convencoes-de-codigo.md`](convencoes-de-codigo.md)
- [ ] `.editorconfig` com as regras de análise
- [ ] `CLAUDE.md` na raiz apontando para as specs (contexto para assistentes)

## Critérios de aceite

- [ ] `dotnet build -c ExportRelease` sem warning nem erro
- [ ] `dotnet test` verde
- [ ] O jogo abre uma cena vazia sem erro no console
- [ ] Os 5 autoloads logam no boot, na ordem correta
- [ ] CI verde em um PR de teste
- [ ] `git lfs ls-files` funciona após adicionar um `.png` qualquer

## Riscos

| Risco | Mitigação |
|---|---|
| Confusão entre edição padrão e .NET do Godot | validar C# rodando **antes** de qualquer outra tarefa |
| Godot headless na CI dá trabalho | manter esse job não-bloqueante no M0; consertar no M9 |
| LFS mal configurado depois de commitar binários | configurar LFS **antes** do primeiro asset |

## Definição de pronto

O próximo dev clona, roda `dotnet build`, abre o editor e começa a trabalhar
sem perguntar nada.
