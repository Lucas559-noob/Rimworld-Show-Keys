#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SOURCE_DIR="$SCRIPT_DIR/Source"
DLL="$SOURCE_DIR/bin/Release/ShowTranslationKeys.dll"
ASSEMBLIES_DIR="$SCRIPT_DIR/Assemblies"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Erro: .NET SDK 8 ou superior não encontrado no PATH." >&2
  exit 1
fi

dotnet build "$SOURCE_DIR/ShowTranslationKeys.csproj" --configuration Release

if [[ ! -f "$DLL" ]]; then
  echo "Erro: DLL não encontrada em: $DLL" >&2
  exit 1
fi

mkdir -p "$ASSEMBLIES_DIR"
cp "$DLL" "$ASSEMBLIES_DIR/ShowTranslationKeys.dll"
echo "Build concluído: $ASSEMBLIES_DIR/ShowTranslationKeys.dll"
