# 03: Testes e CI verde

**What to build:** um pull request passa a ser verificado sozinho. Existe uma
suíte que roda fora da engine e um workflow que compila, testa e reprova
regressão. É o que torna todo ticket seguinte revisável sem abrir o Godot.

**Blocked by:** 01.

**Status:** ready-for-agent

- [ ] `tests/Contenda.Tests` (xUnit, `net8.0`) na solução, com pelo menos um
      teste real passando
- [ ] `tests/Contenda.SceneTests` (GdUnit4) criado, ainda que só com o esqueleto
      — a [spec 01 §2](../../../docs/specs/01-arquitetura-tecnica.md) o exige e
      ele não estava em ticket nenhum
- [ ] `dotnet test` verde localmente e no CI
- [ ] `.github/workflows/ci.yml`: restore → `dotnet build -c ExportRelease` →
      `dotnet test`
- [ ] um warning introduzido de propósito **quebra** o build no CI
- [ ] `.editorconfig` com as regras de análise, e só então
      `EnforceCodeStyleInBuild` religado no `.csproj` — os dois entram juntos
- [ ] job do Godot headless fazendo `--import`, podendo ficar
      `continue-on-error` neste milestone

## Comments

O projeto de testes **não** referencia `GodotSharp` para lógica pura. Se um teste
precisar de `Vector3`, o pacote `GodotSharp` do NuGet resolve sem abrir a engine
— ver [spec 15 §1](../../../docs/specs/15-qualidade-testes-e-performance.md).

`EnforceCodeStyleInBuild` foi deliberadamente desligado no ticket 01: ligado sem
`.editorconfig`, ele detona junto com `TreatWarningsAsErrors` num momento
imprevisível. Religue apenas no mesmo commit que adiciona o `.editorconfig`.
