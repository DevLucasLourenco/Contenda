#!/usr/bin/env bash
#
# Reprova nó 2D em gameplay.
#
# Regra número 1 do Contenda: simulação, colisão, navegação e render são 3D. O
# "2.5D" vem da câmera travada, nunca de sprites.
# Ver docs/specs/02-camera-e-mundo-25d.md §2.
#
# Uso:
#   tools/check-no-2d.sh              verifica o repositório
#   tools/check-no-2d.sh --self-test  prova que o verificador ainda funciona
#
# Saída: 0 limpo · 1 violação encontrada · 2 erro de uso ou do próprio script
#
# PRINCÍPIO: este script falha FECHADO. Qualquer coisa que dê errado nele —
# diretório ausente, nenhum arquivo varrido, erro interno — é saída não-zero.
# Um guarda que erra para o lado do silêncio é pior que guarda nenhum, porque
# produz um passo verde em que todos confiam.

set -uo pipefail

SPEC="docs/specs/02-camera-e-mundo-25d.md"

# Nós 2D proibidos.
#
# Reconhecidos por PADRÃO, não por lista: qualquer tipo de nó terminado em "2D"
# é 2D por definição do Godot. Uma lista à mão esquece AudioStreamPlayer2D,
# RemoteTransform2D, Bone2D, NavigationObstacle2D... e um verificador com furo é
# a mesma disciplina humana que ele existe para substituir.
SUFIXO_2D='[A-Za-z][A-Za-z0-9_]*2D'

# Os 2D que não terminam em "2D" e precisam ser nomeados.
NOMEADOS_2D='TileMap|TileMapLayer|ParallaxBackground|ParallaxLayer|CanvasModulate|CanvasGroup|BackBufferCopy|TouchScreenButton'

BANIDOS="($SUFIXO_2D|$NOMEADOS_2D)"

violacoes=0
arquivos_varridos=0

erro() { # arquivo_relativo linha tipo detalhe
  printf '\n  %s:%s\n' "$1" "$2" >&2
  printf '    nó 2D proibido no gameplay: %s\n' "$3" >&2
  printf '    %s\n' "$4" >&2
  violacoes=$((violacoes + 1))
}

# Uma cena é de interface — e portanto pode usar 2D — se estiver sob "ui/".
# Testado contra o caminho RELATIVO à raiz varrida: contra o absoluto, um
# checkout em /home/ci/ui/repo faria o script pular o repositório inteiro e sair
# com sucesso.
eh_interface() {
  case "$1" in
    ui/*|*/ui/*) return 0 ;;
    *) return 1 ;;
  esac
}

# --- cenas -------------------------------------------------------------------
verificar_cenas() { # raiz
  local raiz="$1" abs rel
  [ -d "$raiz" ] || return 0

  while IFS= read -r abs; do
    rel="${abs#"$raiz"/}"
    arquivos_varridos=$((arquivos_varridos + 1))
    eh_interface "$rel" && continue

    # Nós declarados diretamente.
    while IFS='|' read -r linha tipo nome; do
      [ -n "${linha:-}" ] || continue
      erro "$rel" "$linha" "$tipo" "nó \"${nome:-?}\" — use o equivalente 3D. Ver $SPEC §2."
    done < <(awk -v pat="$BANIDOS" '
      /^\[node / {
        if (match($0, "type=\"" pat "\"")) {
          t = substr($0, RSTART, RLENGTH); gsub(/type="|"/, "", t)
          n = ""; if (match($0, /name="[^"]*"/)) { n = substr($0, RSTART, RLENGTH); gsub(/name="|"/, "", n) }
          print NR "|" t "|" n
        }
      }' "$abs" 2>/dev/null)

    # Sub-cenas de interface instanciadas dentro de gameplay: o nó não declara
    # "type=", então a varredura acima não o enxerga.
    while IFS='|' read -r linha alvo; do
      [ -n "${linha:-}" ] || continue
      erro "$rel" "$linha" "cena de interface" \
        "instancia \"$alvo\", que é de interface, dentro do mundo. Ver $SPEC §2."
    done < <(awk '
      /^\[ext_resource / {
        if (match($0, /path="res:\/\/[^"]*"/)) {
          p = substr($0, RSTART, RLENGTH); gsub(/path="|"/, "", p)
          if (p ~ /\/ui\//) {
            if (match($0, /id="[^"]*"/)) { i = substr($0, RSTART, RLENGTH); gsub(/id="|"/, "", i); ui[i] = p }
          }
        }
      }
      /^\[node .*instance=ExtResource/ {
        if (match($0, /ExtResource\("[^"]*"\)/)) {
          i = substr($0, RSTART, RLENGTH); gsub(/ExtResource\("|"\)/, "", i)
          if (i in ui) print NR "|" ui[i]
        }
      }' "$abs" 2>/dev/null)

  done < <(find "$raiz" -name '*.tscn' -type f 2>/dev/null | sort)
}

# --- scripts -----------------------------------------------------------------
# Classe C# herdando de nó 2D é a outra porta de entrada, e não aparece em cena
# nenhuma até alguém instanciá-la. O awk junta a declaração até a chave de
# abertura, porque a lista de bases costuma quebrar em várias linhas.
verificar_scripts() { # raiz
  local raiz="$1" abs rel
  [ -d "$raiz" ] || return 0

  while IFS= read -r abs; do
    rel="${abs#"$raiz"/}"
    arquivos_varridos=$((arquivos_varridos + 1))

    while IFS='|' read -r linha tipo; do
      [ -n "${linha:-}" ] || continue
      erro "$rel" "$linha" "$tipo" "classe herdando de nó 2D. Ver $SPEC §2."
    done < <(awk -v pat="$BANIDOS" '
      !coletando && /(^|[^A-Za-z0-9_])class[[:space:]]/ { coletando = 1; inicio = NR; buf = "" }
      coletando {
        buf = buf " " $0
        if (buf ~ /\{/ || buf ~ /;/) {
          if (match(buf, ":[[:space:]]*(Godot\\.)?" pat "([^A-Za-z0-9_]|$)")) {
            t = substr(buf, RSTART, RLENGTH)
            gsub(/[:[:space:]]|Godot\./, "", t); gsub(/[^A-Za-z0-9_].*$/, "", t)
            print inicio "|" t
          }
          coletando = 0
        }
      }' "$abs" 2>/dev/null)

  done < <(find "$raiz" -name '*.cs' -type f 2>/dev/null | sort)
}

verificar_repositorio() { # raiz
  local raiz="$1"
  verificar_cenas "$raiz/scenes"
  verificar_cenas "$raiz/data"
  verificar_cenas "$raiz/assets"
  verificar_scripts "$raiz/src"
  verificar_scripts "$raiz/tests"
}

# --- autoteste ---------------------------------------------------------------
# Roda em um diretório temporário ISOLADO, nunca sobre o repositório real: assim
# ele não é afetado por uma violação legítima já existente, e não deixa lixo.
self_test() {
  local tmp falhas=0
  tmp="$(mktemp -d)" || { echo "não consegui criar diretório temporário" >&2; return 2; }
  # shellcheck disable=SC2064
  trap "rm -rf '$tmp'" RETURN

  mkdir -p "$tmp/scenes/arena" "$tmp/scenes/ui" "$tmp/src"

  esperar() { # descricao esperado
    if [ "$violacoes" -eq "$2" ]; then
      printf '  ok: %s\n' "$1"
    else
      printf '  FALHOU: %s (esperava %s, obtive %s)\n' "$1" "$2" "$violacoes" >&2
      falhas=$((falhas + 1))
    fi
  }

  # 1. nó 2D declarado em cena de gameplay
  printf '[gd_scene format=3]\n\n[node name="Mau" type="Sprite2D"]\n' > "$tmp/scenes/arena/a.tscn"
  violacoes=0; verificar_cenas "$tmp/scenes" 2>/dev/null
  esperar "acusa Sprite2D em gameplay" 1

  # 2. 2D fora da lista canônica — o padrão por sufixo tem que pegar
  printf '[gd_scene format=3]\n\n[node name="X" type="AudioStreamPlayer2D"]\n' > "$tmp/scenes/arena/a.tscn"
  violacoes=0; verificar_cenas "$tmp/scenes" 2>/dev/null
  esperar "acusa 2D não listado à mão (AudioStreamPlayer2D)" 1

  # 3. cena de interface pode usar 2D
  rm -f "$tmp/scenes/arena/a.tscn"
  printf '[gd_scene format=3]\n\n[node name="Ok" type="Sprite2D"]\n' > "$tmp/scenes/ui/u.tscn"
  violacoes=0; verificar_cenas "$tmp/scenes" 2>/dev/null
  esperar "NÃO acusa 2D em scenes/ui/" 0

  # 4. sub-cena de interface instanciada no mundo
  printf '[gd_scene format=3]\n\n[ext_resource type="PackedScene" path="res://scenes/ui/u.tscn" id="1_a"]\n\n[node name="Raiz" type="Node3D"]\n\n[node name="Enxerto" parent="." instance=ExtResource("1_a")]\n' > "$tmp/scenes/arena/b.tscn"
  violacoes=0; verificar_cenas "$tmp/scenes" 2>/dev/null
  esperar "acusa cena de interface instanciada no mundo" 1
  rm -f "$tmp/scenes/arena/b.tscn"

  # 5. classe C# herdando de nó 2D
  printf 'using Godot;\npublic partial class Mau : Node2D\n{\n}\n' > "$tmp/src/a.cs"
  violacoes=0; verificar_scripts "$tmp/src" 2>/dev/null
  esperar "acusa classe herdando de Node2D" 1

  # 6. base qualificada
  printf 'public partial class Mau : Godot.Node2D\n{\n}\n' > "$tmp/src/a.cs"
  violacoes=0; verificar_scripts "$tmp/src" 2>/dev/null
  esperar "acusa base qualificada (Godot.Node2D)" 1

  # 7. declaração quebrada em várias linhas
  printf 'public sealed partial class Mau\n    : Node2D,\n      IAlgo\n{\n}\n' > "$tmp/src/a.cs"
  violacoes=0; verificar_scripts "$tmp/src" 2>/dev/null
  esperar "acusa base em múltiplas linhas" 1

  # 8. o caso legítimo não pode ser acusado
  printf 'using Godot;\npublic sealed partial class Bom : CharacterBody3D\n{\n}\n' > "$tmp/src/a.cs"
  violacoes=0; verificar_scripts "$tmp/src" 2>/dev/null
  esperar "NÃO acusa CharacterBody3D" 0

  violacoes=0
  [ "$falhas" -eq 0 ] || { echo "autoteste do verificador FALHOU" >&2; return 1; }
  echo "autoteste do verificador passou (8 casos)"
  return 0
}

case "${1:-}" in
  --self-test) self_test; exit $? ;;
  "") ;;
  *) echo "uso: $0 [--self-test]" >&2; exit 2 ;;
esac

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)" || exit 2
verificar_repositorio "$REPO_ROOT"

# Falhar fechado: se nada foi varrido, algo está errado com o script ou com o
# checkout — e um "nenhuma violação" aqui seria mentira tranquilizadora.
if [ "$arquivos_varridos" -eq 0 ]; then
  echo "erro: nenhum arquivo varrido. O verificador não está olhando o repositório." >&2
  exit 2
fi

if [ "$violacoes" -ne 0 ]; then
  printf '\n%s violação(ões) da regra 3D. O gameplay do Contenda não usa nós 2D.\n' "$violacoes" >&2
  printf 'Interface usa Control e CanvasLayer, em scenes/ui/. Sobreposição no mundo\n' >&2
  printf 'usa Sprite3D ou Control projetado por Camera3D.UnprojectPosition — ver %s §2.\n\n' "$SPEC" >&2
  exit 1
fi

printf 'regra 3D: nenhuma violação (%s arquivos varridos).\n' "$arquivos_varridos"
exit 0
