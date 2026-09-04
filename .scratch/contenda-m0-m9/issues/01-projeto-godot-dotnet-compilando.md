# 01: Projeto Godot .NET compilando

**What to build:** um projeto Godot 4.7.x .NET na raiz do repositório que compila
pela linha de comando, com a estrutura de pastas da spec 01 e o versionamento de
binários configurado. Nenhuma mecânica — é o andaime que todo ticket seguinte usa.

**Blocked by:** None (can start immediately).

**Status:** concluído no commit `3f5c3bd`.

- [x] `dotnet build Contenda.sln -c ExportRelease` sem warning nem erro
- [x] `.sln` declara `Debug`, `ExportDebug` e `ExportRelease`; um `-c Release`
      equivocado falha alto em vez de compilar Debug em silêncio
- [x] `.csproj` com `net8.0`, `Nullable`, `LangVersion latest` e
      `TreatWarningsAsErrors`
- [x] estrutura de `src/`, `scenes/`, `data/`, `assets/`, `tests/`, `tools/`
      conforme spec 01 §2 e §3
- [x] `.gitignore` e `.gitattributes` com as regras de LFS
- [x] **o editor Godot abre o projeto sem erro no console** — verificado em
      2026-09-04, após instalar o Godot 4.7.2 .NET: `--headless --import` roda
      limpo e o Godot **não reescreveu nenhum arquivo versionado**, ou seja,
      aceitou o `project.godot` escrito à mão exatamente como estava.

## Comments

Dois achados registrados nas specs durante a implementação:

1. As configurações do `Godot.NET.Sdk` são `Debug`, `ExportDebug` e
   `ExportRelease` — **não existe `Release`**. O `.sln` gerado por
   `dotnet new sln` mapeia `Release|Any CPU` para `Debug|Any CPU`, então
   `dotnet build -c Release` compilava Debug em silêncio.
2. `dotnet new sln` no SDK .NET 10 gera `.slnx`, formato que o editor Godot não
   lê. Use `dotnet new sln --format sln`.

Code-review aplicado: `Variant` convertido na fronteira com `AsString()`,
versão lida do assembly em vez de redeclarada, e `EnforceCodeStyleInBuild`
desligado até existir `.editorconfig` (ticket 03).
