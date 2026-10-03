#!/usr/bin/env bash
set -euo pipefail

probe="src/Tools/CiWarningBuildProbe.cs"
log="$(mktemp)"
created_probe=0

cleanup() {
  rm -f -- "$log"
  if [[ "$created_probe" -eq 1 ]]; then
    rm -f -- "$probe"
  fi
}
trap cleanup EXIT

if [[ -e "$probe" ]]; then
  printf 'Refusing to overwrite existing probe file: %s\n' "$probe" >&2
  exit 2
fi

created_probe=1
printf '%s\n' '#warning Intentional CI verification warning' > "$probe"

if dotnet build Contenda.sln -c ExportRelease --no-restore > "$log" 2>&1; then
  cat "$log"
  printf 'The build succeeded with an intentional warning.\n' >&2
  exit 1
fi

cat "$log"
if ! grep -q 'error CS1030' "$log"; then
  printf 'The build failed, but not because CS1030 was promoted to an error.\n' >&2
  exit 1
fi

printf 'Confirmed: TreatWarningsAsErrors rejects an intentional C# warning.\n'
