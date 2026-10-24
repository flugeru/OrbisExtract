# OrbisExtract

Cross-platform PlayStation 4 PKG extractor built with C#, .NET, Avalonia UI, and [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg).

## Features

- Read PKG metadata.
- Extract package contents to a selected destination.
- Optional passcode input for packages that require it.
- Windows, Linux, and Android project targets.

## Requirements

- .NET SDK 8 for the shared backend and desktop project.
- .NET SDK 10 and the Android workload for Android builds.
- Java and Android SDK for Android builds.

## Build

```bash
dotnet restore OrbisExtract.sln
dotnet build OrbisExtract.sln
dotnet test OrbisExtract.sln --no-restore
```

## Publish

- Windows: `powershell -ExecutionPolicy Bypass -File scripts/publish-windows.ps1`
  - **Note**: First, create `Icon.ico` from `src/OrbisExtract.App/OrbisExtract.App/Assets/Icon.png` using ImageMagick or an online converter. Run `scripts/create-windows-icon.ps1` to automate this.
- Linux: `bash scripts/publish-linux.sh`
- AppImage: `bash scripts/build-appimage.sh` after publishing Linux; `appimagetool` must be installed.
- Android: `bash scripts/publish-android.sh` in an environment configured with the .NET 10 Android workload, Java, and Android SDK.

The GitHub Actions workflow builds the Android APK and Windows application. Download build artifacts from the repository's [Actions](https://github.com/flugeru/OrbisExtract/actions) page.

## Notes

- Extraction is limited to packages the underlying library can read and decrypt. Required passcodes or keys must be supplied; the application does not bypass them.
- No PKG sample is included. End-to-end extraction on real packages and Android device validation are not currently claimed as tested.
- The project does not create, convert, install, download, or manage PKG files.

## License

See [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
