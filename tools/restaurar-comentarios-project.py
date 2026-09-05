"""Devolve ao project.godot os comentarios que o editor Godot apaga ao salvar.

O editor reescreve o arquivo inteiro e descarta todo comentario. As explicacoes
perdidas nao sao decorativas: a ordem dos autoloads e load-bearing, e o espelho
das camadas em GameConstants so existe se alguem souber que ele existe.
"""
import io
import os

os.chdir(r"C:\Users\lucas\OneDrive\Documentos\Github Repo\Contenda")
p = "project.godot"
s = io.open(p, encoding="utf-8").read()

cabecalho = """; Configuração do projeto Contenda.
;
; ATENÇÃO: o editor Godot REESCREVE este arquivo ao salvar pela interface, e ao
; fazê-lo APAGA TODOS OS COMENTÁRIOS. Se eles sumirem, restaure com
; tools/restaurar-comentarios-project.py — as explicações abaixo não são
; decorativas.

config_version=5"""
s = s.replace("config_version=5", cabecalho, 1)

s = s.replace(
    "[autoload]\n\nGameEvents=",
    "[autoload]\n\n"
    "; A ORDEM IMPORTA. Autoloads recebem _Ready na ordem em que aparecem aqui, e\n"
    "; GameBootstrap é o único que usa os outros — por isso vem por último.\n"
    "; Registrá-lo primeiro faria o ServiceLocator devolver nulo no boot.\n"
    "GameEvents=", 1)

s = s.replace(
    '[layer_names]\n\n3d_physics/layer_1=',
    "[layer_names]\n\n"
    "; Espelhado em src/Core/GameConstants.cs — PhysicsLayers. Os dois lados andam\n"
    "; juntos, e um teste em Contenda.Tests reprova a divergência.\n"
    "; Ver docs/specs/01-arquitetura-tecnica.md §8.\n"
    "3d_physics/layer_1=", 1)

s = s.replace(
    "[navigation]\n\n3d/default_cell_size=",
    "[navigation]\n\n"
    "; Precisa bater com cell_size/cell_height da navmesh em data/navmesh/.\n"
    "; Divergir faz o servidor avisar e degrada a precisão do caminho.\n"
    "3d/default_cell_size=", 1)

# O editor removeu esta linha por ser o padrão da engine. Deixá-la implícita
# significa que uma mudança de padrão numa versão futura trocaria o renderer sem
# ninguém perceber — e a spec 01 §1 fixa Forward+.
if 'renderer/rendering_method="forward_plus"' not in s:
    s = s.replace(
        "[rendering]\n\nrenderer/rendering_method.mobile=",
        "[rendering]\n\n"
        "; Explícito de propósito: o editor omite por ser o padrão atual, mas a\n"
        "; spec 01 §1 fixa Forward+ e um padrão implícito muda sem aviso.\n"
        'renderer/rendering_method="forward_plus"\n'
        "renderer/rendering_method.mobile=", 1)

io.open(p, "w", encoding="utf-8", newline="\n").write(s)

print("comentários restaurados:", s.count("; "))
print("renderer explícito:", 'renderer/rendering_method="forward_plus"' in s)
