# Android App Bundle for Google Play

The existing APK workflow is for direct installation. The separate **Android AAB (Google Play upload)** workflow builds a signed bundle on a manual run from `main` only. It does not upload to Google Play automatically.

## Create and back up the upload key

On Windows, with the JDK installed, run `keytool` in a private folder **outside this repository**:

```powershell
keytool -genkeypair -v -keystore orbisextract-upload.keystore -alias orbisextract -keyalg RSA -keysize 2048 -validity 10000
```

Choose the keystore password interactively. If prompted for a separate key password, use the same password (the default PKCS12 format typically uses one password). Back up the keystore and password privately; do not commit them, attach them to an issue/release, or paste them into chat. Keep the key for future updates to this Play listing. Enable Play App Signing in the Play Console; the key above is the **upload key**, not the Play app-signing key.

## GitHub Actions secrets

In the repository's **Settings → Secrets and variables → Actions → New repository secret**, add:

| Secret | Value |
| --- | --- |
| `ANDROID_UPLOAD_KEYSTORE_BASE64` | Base64 encoding of the entire `.keystore` file |
| `ANDROID_UPLOAD_KEY_ALIAS` | Alias entered with `keytool -alias` (example: `orbisextract`) |
| `ANDROID_UPLOAD_STORE_PASSWORD` | Keystore password |
| `ANDROID_UPLOAD_KEY_PASSWORD` | Key password (same as store password if you chose the same one) |

To copy the keystore's Base64 data to your Windows clipboard without writing another file:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\path\to\orbisextract-upload.keystore')) | Set-Clipboard
```

Paste that value directly into the GitHub secret. Clear the clipboard after saving it (`Set-Clipboard -Value ''`). Do not run the workflow with secrets supplied by an untrusted contributor, and review changes to the workflow before running it.

## Build

Open **Actions → Android AAB (Google Play upload) → Run workflow** and select `main`. The job publishes only `OrbisExtract-play-upload.aab` in the `OrbisExtract-Android-Play-AAB` artifact (available for 14 days). The keystore and password files are created only on the runner, removed after the build, and are not uploaded as artifacts. The .NET Android AAB signer uses `file:` password references rather than `env:`.

Before uploading, confirm the app ID `orbisextract.app` and the Play Console listing use the same package ID. Each subsequent Play upload must have a **higher AndroidPackageVersion (versionCode)** than any previous upload. The user-visible version is `ApplicationDisplayVersion`.

An APK distributed outside Google Play may not update in-place from Google Play if its signing certificate differs. Test the transition with an internal testing track before public rollout.
