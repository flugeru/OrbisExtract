#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APPDIR="$ROOT/artifacts/OrbisExtract.AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/256x256/apps"
cp -a "$ROOT/artifacts/linux-x64/." "$APPDIR/usr/bin/"
cp "$ROOT/packaging/linux/orbisextract.desktop" "$APPDIR/usr/share/applications/"
cp "$ROOT/packaging/linux/orbisextract.desktop" "$APPDIR/"
appimagetool "$APPDIR" "$ROOT/artifacts/OrbisExtract-x86_64.AppImage"
