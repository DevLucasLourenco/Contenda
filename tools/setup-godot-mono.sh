#!/usr/bin/env bash
set -euo pipefail

version="${GODOT_VERSION:-4.7.2}"
install_templates="${GODOT_INSTALL_EXPORT_TEMPLATES:-false}"
install_dir="$RUNNER_TEMP/godot-mono-$version"
archive="$RUNNER_TEMP/godot-mono-$version.zip"
binary_url="https://downloads.godotengine.org/?flavor=stable&platform=linux.64&slug=mono_linux_x86_64.zip&version=$version"

mkdir -p "$install_dir"
curl --fail --location --retry 3 "$binary_url" --output "$archive"
unzip -q -o "$archive" -d "$install_dir"

binary="$(find "$install_dir" -type f -name "Godot_v$version-stable_mono_linux.*" -print -quit)"
if [[ -z "$binary" ]]; then
    echo "Godot .NET $version Linux binary not found in downloaded archive." >&2
    exit 1
fi
chmod +x "$binary"

if [[ "$install_templates" == "true" ]]; then
    templates_archive="$RUNNER_TEMP/godot-mono-templates-$version.tpz"
    templates_url="https://downloads.godotengine.org/?flavor=stable&platform=templates&slug=mono_export_templates.tpz&version=$version"
    templates_temp="$RUNNER_TEMP/godot-mono-templates-$version"
    templates_dir="$HOME/.local/share/godot/export_templates/$version.stable.mono"
    mkdir -p "$templates_temp" "$templates_dir"
    curl --fail --location --retry 3 "$templates_url" --output "$templates_archive"
    unzip -q -o "$templates_archive" -d "$templates_temp"
    template_source="$(find "$templates_temp" -type f -name 'linux_release.x86_64' -printf '%h\n' -quit)"
    if [[ -z "$template_source" ]]; then
        echo "Godot .NET $version release templates not found in downloaded archive." >&2
        exit 1
    fi
    cp -a "$template_source/." "$templates_dir/"
fi

echo "GODOT_BIN=$binary" >> "$GITHUB_ENV"
