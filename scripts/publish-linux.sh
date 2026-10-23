#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/OrbisExtract.App/OrbisExtract.App.Desktop/OrbisExtract.App.Desktop.csproj"
OUT="$ROOT/artifacts/linux-x64"
dotnet publish "$PROJECT" -c Release -r linux-x64 --self-contained true -o "$OUT"
printf 'Publicado em %s\n' "$OUT"
