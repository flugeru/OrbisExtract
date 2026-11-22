#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
: "${ANDROID_UPLOAD_KEYSTORE:?Set the absolute path to the upload keystore}"
: "${ANDROID_UPLOAD_PASSWORD_FILE:?Set the absolute path to the password file}"
: "${ANDROID_VERSION_CODE:?Set a new versionCode above ALL previously distributed Android builds}"
: "${ANDROID_SDK_ROOT:?Set Android SDK path}"
[[ "$ANDROID_VERSION_CODE" =~ ^[1-9][0-9]*$ ]] && (( ANDROID_VERSION_CODE <= 2100000000 ))
test -f "$ANDROID_UPLOAD_KEYSTORE"
test -s "$ANDROID_UPLOAD_PASSWORD_FILE"
OUT="${ORBIS_OUTPUT_DIR:-$ROOT/artifacts/play}"
dotnet publish "$ROOT/src/OrbisExtract.App/OrbisExtract.App.Android/OrbisExtract.App.Android.csproj" \
  -f net10.0-android36.0 -c Release -m:1 -p:UseSharedCompilation=false \
  -p:DistributionChannel=Play -p:AndroidPackageFormats=aab \
  -p:ApplicationVersion="$ANDROID_VERSION_CODE" \
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore="$ANDROID_UPLOAD_KEYSTORE" \
  -p:AndroidSigningKeyAlias="${ANDROID_UPLOAD_ALIAS:-orbisextract-upload}" \
  -p:AndroidSigningKeyPass="file:$ANDROID_UPLOAD_PASSWORD_FILE" \
  -p:AndroidSigningStorePass="file:$ANDROID_UPLOAD_PASSWORD_FILE" \
  -p:AndroidSdkDirectory="$ANDROID_SDK_ROOT" -o "$OUT"
AAB="$(find "$OUT" -maxdepth 1 -name '*-Signed.aab' -print -quit)"
test -n "$AAB"
jarsigner -verify "$AAB"
echo "Signed Play bundle: $AAB"
