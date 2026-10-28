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

## GitHub Actions APK updates (important)

Android only allows an APK to update an installed app when the **application ID** and **signing certificate** match, and the new versionCode is higher. The Actions workflow now requires a persistent signing key rather than relying on a fresh runner's default/debug signing key.

Configure these repository Actions secrets before running the Android build:

- `ANDROID_KEYSTORE_BASE64`: base64 of the **same keystore used to sign the installed 1.0.2 APK** (not a newly generated keystore).
- `ANDROID_KEYSTORE_PASSWORD`: keystore password.
- `ANDROID_KEY_ALIAS`: alias of the original signing key.
- `ANDROID_KEY_PASSWORD`: key password.

For example, in PowerShell, generate the base64 secret from the original keystore with `[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\\path\\to\\original.keystore'))`. Paste only into GitHub Actions secrets, never into source control or a chat. Keep an offline backup of the keystore and passwords. The workflow fails instead of publishing an APK signed by an unintended key when the secrets are missing.

**Existing 1.0.2 installations:** If 1.0.2 was signed with an ephemeral GitHub Actions debug keystore and that exact key was not preserved, an in-place update is cryptographically impossible. Back up any user data before uninstalling and reinstalling once with the persistent-key build. Future releases signed with that same key can update in place. A different application ID (such as legacy `orbisextract.app`) also requires migration/reinstallation.

Check installed and new APKs with `apksigner verify --print-certs path/to/app.apk` and compare the signer certificate SHA-256 digest. Check package name and versionCode with `aapt dump badging path/to/app.apk`.
