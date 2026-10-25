#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APPDIR="$ROOT/artifacts/OrbisExtract.AppDir"
ICON="$ROOT/src/OrbisExtract.App/OrbisExtract.App/Assets/Icon.png"
APPIMAGETOOL="${APPIMAGETOOL:-appimagetool}"
BIN="OrbisExtract.App.Desktop"

if [ ! -d "$ROOT/artifacts/linux-x64" ]; then
  echo "artifacts/linux-x64 não existe — rode scripts/publish-linux.sh antes." >&2
  exit 1
fi
if [ ! -f "$ICON" ]; then
  echo "Ícone não encontrado: $ICON" >&2
  exit 1
fi

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/256x256/apps"
cp -a "$ROOT/artifacts/linux-x64/." "$APPDIR/usr/bin/"
chmod +x "$APPDIR/usr/bin/$BIN"
cp "$ROOT/packaging/linux/orbisextract.desktop" "$APPDIR/usr/share/applications/"
cp "$ROOT/packaging/linux/orbisextract.desktop" "$APPDIR/"
cp "$ICON" "$APPDIR/orbisextract.png"
cp "$ICON" "$APPDIR/usr/share/icons/hicolor/256x256/apps/orbisextract.png"

export VERSION="${VERSION:-$(git -C "$ROOT" describe --tags --always 2>/dev/null || echo dev)}"

"$APPIMAGETOOL" "$APPDIR" "$ROOT/artifacts/OrbisExtract-x86_64.AppImage"
printf 'AppImage gerado em %s\n' "$ROOT/artifacts/OrbisExtract-x86_64.AppImage"
