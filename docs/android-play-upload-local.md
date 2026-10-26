# Sign an Android AAB locally for Google Play

The GitHub Actions build produces an installable APK. The upload key and AAB signing remain **on your own Windows computer**, not in GitHub secrets or CI artifacts.

## First-time setup

Install the .NET 10 Android workload, JDK 17, and Android SDK command-line tools/platforms. Keep the upload key **outside the repository**, for example in a private folder in your user profile. Create it **once**, from your own interactive PowerShell terminal:

```powershell
$private = Join-Path $env:USERPROFILE 'OrbisExtract-private'
New-Item -ItemType Directory -Force $private
keytool -genkeypair -v -keystore (Join-Path $private 'orbisextract-upload.keystore') -alias orbisextract -keyalg RSA -keysize 2048 -validity 10000
```

`keytool` asks you to choose the password and certificate information directly in your terminal. Do not give the password to anyone or paste it into chat. Back up the `.keystore` and password securely; you need the same upload key for later versions. Enable **Play App Signing** in the Play Console; this local key is the *upload key*, not the Play app-signing key.

## Build and verify

From the repository root, run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-android-aab.ps1 -KeyStore "$env:USERPROFILE\OrbisExtract-private\orbisextract-upload.keystore"
```

The script prompts for both passwords without echoing them, passes them to the .NET Android signer through temporary `file:` references, verifies the signed AAB using `jarsigner`, and removes the temporary password files. Only run this on your trusted computer. The signed bundle is written to `artifacts/play-upload/` (ignored by Git); upload that **signed** `.aab` to Play Console, not an unsigned bundle. Do not attach the keystore to a release or commit it. On Windows, this script must be run in a normal interactive terminal so you can type your passwords.

The Play package ID is `orbisextract.app`. Each subsequent upload must have a higher `AndroidPackageVersion` (versionCode) than previous Play uploads; `ApplicationDisplayVersion` is the user-visible version. An app installed from an APK distributed outside Play may not update in place from Play when signing certificates differ: test the transition on an internal testing track.
