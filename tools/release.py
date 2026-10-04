#!/usr/bin/env python3
"""Validate the tagged release version and assemble clean Contenda packages."""

from __future__ import annotations

import argparse
import re
import sys
import tempfile
import xml.etree.ElementTree as ElementTree
import zipfile
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[1]
TAG_PATTERN = re.compile(r"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")
FORBIDDEN_PARTS = {"docs", "tests", "tools"}
FORBIDDEN_SUFFIXES = {".cs", ".csproj", ".sln", ".slnx", ".pdb", ".mdb"}


def fail(message: str) -> None:
    raise ValueError(message)


def read_project_version() -> str:
    project_text = (ROOT / "project.godot").read_text(encoding="utf-8")
    match = re.search(r'^config/version="([^"]+)"$', project_text, re.MULTILINE)
    if match is None:
        fail("project.godot is missing application/config/version.")

    project_version = match.group(1)
    project_xml = ElementTree.parse(ROOT / "Contenda.csproj")
    assembly_version = project_xml.findtext(".//Version")
    if not assembly_version:
        fail("Contenda.csproj is missing <Version>.")
    if assembly_version != project_version:
        fail(
            "Version mismatch: project.godot has "
            f"{project_version}, but Contenda.csproj has {assembly_version}."
        )

    return project_version


def validate_tag(tag: str) -> str:
    match = TAG_PATTERN.fullmatch(tag)
    if match is None:
        fail(f"Release tag {tag!r} must use the form vMAJOR.MINOR.PATCH.")

    tag_version = ".".join(match.groups())
    project_version = read_project_version()
    if tag_version != project_version:
        fail(
            f"Release tag is {tag_version}, but project.godot and "
            f"Contenda.csproj are {project_version}."
        )

    return project_version


def reject_development_files(root: Path) -> None:
    for path in root.rglob("*"):
        relative = PurePosixPath(path.relative_to(root).as_posix())
        parts = {part.lower() for part in relative.parts}
        if parts & FORBIDDEN_PARTS:
            fail(f"Development-only path included in export: {relative}")
        if "debug" in parts or relative.suffix.lower() in FORBIDDEN_SUFFIXES:
            fail(f"Debug or development file included in export: {relative}")
        if relative.name.lower().endswith(".dsym"):
            fail(f"Debug symbols included in export: {relative}")


def readme_text(version: str) -> str:
    return f"""CONTENDA {version}

Jogo de ação single-player em arena. Esta versão inclui o modo Horde, dois
personagens jogáveis e progressão por ondas.

Como iniciar
Windows: abra Contenda.exe.
Linux: execute Contenda.x86_64.
macOS: abra Contenda.app. Esta versão não é assinada nem notarizada; o macOS
pode pedir confirmação para abrir o aplicativo.

Controles padrão
WASD: mover e registrar comandos.
Mouse: mirar.
Botão esquerdo: ataque básico.
Botão direito: confirmar uma habilidade pela sequência digitada.
Roda do mouse: selecionar transformação.
Botão do meio: ativar transformação.
Espaço: pular. Shift: dash. Esc: pausar.

As configurações ficam na pasta de dados do usuário do sistema operacional.
Consulte LICENSE e THIRD-PARTY-NOTICES.md para os termos de uso. O arquivo
GODOT_COPYRIGHT.txt contém licenças do motor e de suas dependências.

Limitações conhecidas
- Não existe versão para navegador; o projeto usa Godot .NET/C#.
- O desempenho-alvo de 60 fps e p99 não foi validado no hardware de referência.
  A otimização adicional foi adiada para depois desta prioridade de release.
"""


def add_tree(archive: zipfile.ZipFile, source: Path, archive_prefix: str) -> None:
    files = sorted(path for path in source.rglob("*") if path.is_file())
    for path in files:
        relative = path.relative_to(source).as_posix()
        archive.write(path, f"{archive_prefix}/{relative}")


def package_release(tag: str, raw_dir: Path, output_dir: Path) -> list[Path]:
    version = validate_tag(tag)
    license_path = ROOT / "LICENSE"
    notices_path = ROOT / "THIRD-PARTY-NOTICES.md"
    godot_copyright_path = ROOT / "build" / "GODOT_COPYRIGHT.txt"
    if not license_path.is_file() or license_path.stat().st_size == 0:
        fail("LICENSE is missing; the release is blocked until the user chooses the code license.")
    if not notices_path.is_file() or notices_path.stat().st_size == 0:
        fail("THIRD-PARTY-NOTICES.md is missing or empty.")
    if not godot_copyright_path.is_file() or godot_copyright_path.stat().st_size == 0:
        fail("GODOT_COPYRIGHT.txt from the Godot export version is missing or empty.")

    raw_dir = raw_dir if raw_dir.is_absolute() else ROOT / raw_dir
    output_dir = output_dir if output_dir.is_absolute() else ROOT / output_dir
    output_dir.mkdir(parents=True, exist_ok=True)

    packages = [
        ("win64", raw_dir / "windows", "Contenda.exe"),
        ("linux-x86_64", raw_dir / "linux", "Contenda.x86_64"),
        ("macos-universal", raw_dir / "macos", "Contenda.app"),
    ]
    outputs: list[Path] = []
    for platform, source_dir, executable_name in packages:
        export_root = source_dir / executable_name
        if not export_root.exists():
            fail(f"Missing {platform} export at {export_root}.")
        if platform == "macos-universal" and not (
            export_root / "Contents" / "MacOS" / "Contenda"
        ).is_file():
            fail(f"The macOS app bundle is incomplete: {export_root}.")

        reject_development_files(source_dir)
        if platform == "macos-universal":
            app_executable = export_root / "Contents" / "MacOS" / "Contenda"
            app_executable.chmod(app_executable.stat().st_mode | 0o111)

        package_name = f"Contenda-{version}-{platform}.zip"
        output_path = output_dir / package_name
        with tempfile.NamedTemporaryFile(
            prefix=f"{package_name}.", suffix=".tmp", dir=output_dir, delete=False
        ) as temporary:
            temporary_path = Path(temporary.name)

        try:
            with zipfile.ZipFile(
                temporary_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9
            ) as archive:
                if export_root.is_dir():
                    add_tree(archive, export_root, executable_name)
                else:
                    archive.write(export_root, executable_name)
                archive.write(license_path, "LICENSE")
                archive.write(notices_path, "THIRD-PARTY-NOTICES.md")
                archive.write(godot_copyright_path, "GODOT_COPYRIGHT.txt")
                archive.writestr("README.txt", readme_text(version))
            temporary_path.replace(output_path)
        except Exception:
            temporary_path.unlink(missing_ok=True)
            raise

        outputs.append(output_path)

    return outputs


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    subcommands = parser.add_subparsers(dest="command", required=True)

    validate = subcommands.add_parser("validate", help="check tag and project versions")
    validate.add_argument("--tag", required=True)

    package = subcommands.add_parser("package", help="make the three versioned ZIP files")
    package.add_argument("--tag", required=True)
    package.add_argument("--raw-dir", type=Path, default=Path("build/raw"))
    package.add_argument("--output-dir", type=Path, default=Path("build/packages"))
    return parser.parse_args()


def main() -> int:
    arguments = parse_arguments()
    try:
        if arguments.command == "validate":
            print(f"Release version {validate_tag(arguments.tag)} is consistent.")
            return 0

        outputs = package_release(arguments.tag, arguments.raw_dir, arguments.output_dir)
        for output in outputs:
            print(output)
        return 0
    except (OSError, ValueError, ElementTree.ParseError, zipfile.BadZipFile) as error:
        print(f"Release packaging failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
