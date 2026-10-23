#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/OrbisExtract.App/OrbisExtract.App.Android/OrbisExtract.App.Android.csproj"
if ! dotnet --list-sdks | grep -q '^10\.'; then
  echo 'SDK .NET 10 é obrigatório para Avalonia Android; instale-o antes de publicar.' >&2
  exit 2
fi
dotnet workload list | grep -q '^android' || { echo 'Instale o workload Android com: dotnet workload install android' >&2; exit 2; }
dotnet publish "$PROJECT" -f net10.0-android -c Release -p:AndroidPackageFormat=apk -o "$ROOT/artifacts/android"
