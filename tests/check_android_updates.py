"""Run with python tests/check_android_updates.py; no extra dependencies."""
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
workflow = (root / ".github/workflows/android-apk.yml").read_text(encoding="utf-8")
project = ET.parse(root / "src/OrbisExtract.App/OrbisExtract.App.Android/OrbisExtract.App.Android.csproj")
assert project.findtext(".//ApplicationId") == "com.tatsuya.orbisextract"
for required in (
    "key: orbisextract-development-signing-v1",
    "1040000 + GITHUB_RUN_NUMBER",
    "-p:ApplicationVersion=${{ env.APK_VERSION_CODE }}",
    "-p:AndroidKeyStore=true",
    '-p:AndroidSigningKeyStore="$HOME/.local/share/orbisextract-signing/development.jks"',
    "versionCode='$APK_VERSION_CODE'",
    'certificate SHA-256 digest: $fingerprint',
):
    assert required in workflow, required
assert "versionCode='1040000'" not in workflow
print("Android update signing/version configuration checks passed")
