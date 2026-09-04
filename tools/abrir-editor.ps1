# Abre o Contenda no editor Godot CORRETO.
#
# O projeto é C#, e só a edição .NET do Godot roda C#. Abrir com a edição padrão
# faz o editor perguntar "Abrir assim mesmo? Projeto será modificado" — e
# continuar REMOVE a configuração de C# do project.godot, quebrando tudo.
#
# A diferença está no nome do arquivo, e é fácil de não ver:
#   Godot_v4.7.2-stable_win64.exe        ← padrão, SEM C#
#   Godot_v4.7.2-stable_mono_win64.exe   ← .NET, é esta
#
# Uso:  pwsh tools/abrir-editor.ps1        (ou dê duplo clique em abrir-editor.cmd)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot

$candidatos = @(
    "$env:LOCALAPPDATA\Microsoft\WinGet\Packages",
    "$env:LOCALAPPDATA\Programs",
    "$env:ProgramFiles"
) | Where-Object { Test-Path $_ }

$editor = $candidatos |
    ForEach-Object { Get-ChildItem $_ -Recurse -Filter "*mono*win64.exe" -ErrorAction SilentlyContinue -Depth 4 } |
    Where-Object { $_.Name -notlike "*console*" } |
    Select-Object -First 1

if (-not $editor) {
    Write-Host "Não encontrei a edição .NET do Godot." -ForegroundColor Red
    Write-Host ""
    Write-Host "Instale com:  winget install --id GodotEngine.GodotEngine.Mono --exact"
    Write-Host ""
    Write-Host "NÃO use um Godot cujo nome não tenha 'mono': ele não roda C# e"
    Write-Host "oferece modificar o projeto para remover a configuração .NET."
    exit 1
}

Write-Host "Editor:  $($editor.FullName)" -ForegroundColor Green
Write-Host "Projeto: $raiz" -ForegroundColor Green
Start-Process -FilePath $editor.FullName -ArgumentList @("--path", $raiz, "--editor")
