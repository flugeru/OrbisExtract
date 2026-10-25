#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APPDIR="$ROOT/artifacts/OrbisExtract.AppDir"
ICON="$ROOT/packaging/linux/orbisextract.png"
BIN="OrbisExtract.App.Desktop"
APPIMAGETOOL="${APPIMAGETOOL:-appimagetool}"

# Fallback local caso o ícone de packaging ainda não exista
if [ ! -f "$ICON" ] && [ -f "$ROOT/src/OrbisExtract.App/OrbisExtract.App/Assets/Icon.png" ]; then
  ICON="$ROOT/src/OrbisExtract.App/OrbisExtract.App/Assets/Icon.png"
fi

if [ ! -d "$ROOT/artifacts/linux-x64" ]; then
  echo "artifacts/linux-x64 não existe — rode scripts/publish-linux.sh antes." >&2
  exit 1
fi
if [ ! -f "$ICON" ]; then
  echo "Ícone não encontrado: $ICON" >&2
  exit 1
fi
if [ ! -x "$ROOT/artifacts/linux-x64/$BIN" ]; then
  echo "Binário não encontrado ou sem permissão de execução: artifacts/linux-x64/$BIN" >&2
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

# AppRun é obrigatório: é o entrypoint que o runtime do AppImage executa
# e o que ferramentas de integração (Gear Lever etc.) procuram.
cat > "$APPDIR/AppRun" <<EOF
#!/bin/sh
SELF="\$(readlink -f "\$0")"
HERE="\${SELF%/*}"
export APPDIR="\$HERE"
export PATH="\$HERE/usr/bin:\$PATH"
exec "\$HERE/usr/bin/$BIN" "\$@"
EOF
chmod +x "$APPDIR/AppRun"

export VERSION="${VERSION:-$(git -C "$ROOT" describe --tags --always 2>/dev/null || echo dev)}"

"$APPIMAGETOOL" "$APPDIR" "$ROOT/artifacts/OrbisExtract-x86_64.AppImage"

# Verificação: o AppImage gerado precisa conter AppRun, desktop e ícone
if [ ! -x "$APPDIR/AppRun" ]; then
  echo "AppRun ausente no AppDir após empacotamento." >&2
  exit 1
fi

printf 'AppImage gerado em %s\n' "$ROOT/artifacts/OrbisExtract-x86_64.AppImage"
