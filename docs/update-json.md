# Update manifest

The app reads `update.json` from the public `main` branch at
`https://raw.githubusercontent.com/flugeru/OrbisExtract/main/update.json`. It checks
once when the view opens and when the user selects **Verificar atualizações** in
**Sobre**. The prompt is optional: **Agora não** dismisses it. No APK or desktop
installer is downloaded or installed by the app.

The file starts at `1.0.1` while version `1.0.2` is being built. **Only change
`version` to `1.0.2` after the relevant downloads are actually available** on
GitHub and Play. Otherwise users may be sent to a page without an update.

Example:

```json
{
  "version": "1.0.2",
  "androidPlayUrl": "https://play.google.com/store/apps/details?id=com.tatsuya.orbisextract",
  "androidGithubUrl": "https://github.com/flugeru/OrbisExtract/releases/latest",
  "windowsUrl": "https://github.com/flugeru/OrbisExtract/releases/latest",
  "linuxUrl": "https://github.com/flugeru/OrbisExtract/releases/latest"
}
```

The Android installer source determines which link to open: `com.android.vending`
opens Play; other or unknown sources open GitHub. An APK from GitHub **cannot
update a Play-installed app in place** if its signing certificate differs, even
if the package ID matches. GitHub Actions builds an APK, not a Play-signed AAB.

The JSON link is informational, not an installation mechanism; always verify
that the version and compatible download are published before advertising them.
