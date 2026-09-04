@echo off
REM Duplo clique aqui abre o Contenda no editor Godot .NET correto.
REM Ver tools/abrir-editor.ps1 para o porquê de isto existir.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0abrir-editor.ps1"
if errorlevel 1 pause
