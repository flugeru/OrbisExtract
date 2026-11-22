# Update manifest

The app reads `update.json` from the public `main` branch at
`https://raw.githubusercontent.com/flugeru/OrbisExtract/main/update.json`. It checks
once when the view opens and when the user selects **Verificar atualizações** in
**Sobre**. The prompt is optional: **Agora não** dismisses it. No APK or desktop
installer is downloaded or installed by the app.

The public manifest tracks the published release. For 1.0.5, publish the
downloads first, then copy [update-1.0.5.json](update-1.0.5.json) over
`update.json` on `main` to announce the update.

Example:

```json
{
  "version": "1.0.2",
  "releaseNotes": [
    "Nova splash screen no Android com o ícone do OrbisExtract.",
    "Verificação opcional de atualizações em Sobre."
  ],
  "releaseNotesEn": [
    "New Android splash screen with the OrbisExtract icon.",
    "Optional update checks in About."
  ],
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
`releaseNotes` is an optional array of short strings shown in the update prompt
by builds that support it. Replace the notes with the real changelog for the
announced version when publishing. Older builds (including desktop 1.0.2)
ignore this new field and continue to show only the version and download link.
`releaseNotesEn` is optional for English users on 1.0.3 and later; if absent,
the app displays `releaseNotes` verbatim. Provide both languages before publishing.
