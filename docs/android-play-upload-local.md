# Sign an Android AAB locally for Google Play

The GitHub Actions build produces an installable APK. The upload key and AAB signing remain **on your own Windows computer**, not in GitHub secrets or CI artifacts.

## First-time setup

Install the .NET 10 Android workload, JDK 17, and Android SDK command-line tools/platforms. Keep the upload key **outside the repository**, for example in a private folder in your user profile. Create it **once**, from your own interactive PowerShell terminal:

```powershell
$private = Join-Path $env:USERPROFILE 'OrbisExtract-private'
New-Item -ItemType Directory -Force $private
keytool -genkeypair -v -keystore (Join-Path $private 'orbisextract-upload.keystore') -alias Tatsuya -keyalg RSA -keysize 2048 -validity 10000
```

`Tatsuya` is the key **alias**, not automatically the certificate's owner name. When `keytool` prompts for your name, enter `Tatsuya` if that is the owner name you agreed on. It asks you to choose the password and certificate information directly in your terminal. Do not give the password to anyone or paste it into chat. Back up the `.keystore` and password securely; you need the same upload key for later versions. Enable **Play App Signing** in the Play Console; this local key is the *upload key*, not the Play app-signing key.

## Build and verify

From the repository root, run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-android-aab.ps1 -KeyStore "$env:USERPROFILE\OrbisExtract-private\orbisextract-upload.keystore"
```

The script prompts for both passwords without echoing them, passes them to the .NET Android signer through temporary `file:` references, cleans previous Release build outputs, verifies the AAB integrity and compares its signer certificate with the upload keystore certificate. It rejects a debug-signed or mismatched AAB even if `jarsigner` reports `jar verified`. It removes the temporary password files. Only run this on your trusted computer. The signed bundle is written to `artifacts/play-upload/` (ignored by Git); share **only** the `.aab` from a run that prints `Signed AAB ready`. Do not attach the keystore to a release or commit it. On Windows, this script must be run in a normal interactive terminal so you can type your passwords.

The Play package ID is `com.tatsuya.orbisextract`, minimum SDK is API 24 and target SDK is API 36. This is a **different app** from the previously distributed `orbisextract.app` APK, so it cannot update that installation in place. Confirm the package ID with the Play Console owner before the first upload: a Play listing's package ID cannot be changed later. Each subsequent upload must have a higher `ApplicationVersion` (versionCode) than previous Play uploads; `ApplicationDisplayVersion` is the user-visible version.

## GitHub Actions APK signing (separate from Google Play)

The Actions workflow uses a dedicated GitHub-distribution signing identity, **not** your Google Play upload key. The private keystore is deliberately NOT committed to this public repository. Add the generated base64 keystore as a single Actions repository secret named `ORBIS_APK_KEYSTORE_BASE64` in Settings > Secrets and variables > Actions > New repository secret. Keep a private backup of the keystore; never publish it in the repository or attach it to releases.

The workflow checks the signer certificate SHA-256 fingerprint: `064aa88f9483b842c8ae83185e53efcb3a70d4f1c1932c44297f54a0077e3c5b`. Future Actions APKs using this key and increasing versionCodes can update each other in place. The original 1.0.2 APK can only be updated if its signing certificate already matches; if it used an ephemeral runner key, reinstall once after backing up user data.

To inspect an APK: `apksigner verify --print-certs OrbisExtract.apk`; compare signer SHA-256. `aapt dump badging OrbisExtract.apk` shows the package name and versionCode.
