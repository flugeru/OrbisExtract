# OrbisExtract

A cross-platform PlayStation 4 `.PKG` extractor with an Avalonia UI and a C# backend based on commit `92a4e7f8e5914cb06b52ba3ae6b0675da438a051` of [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg).

## Current status and limitations

The integrated engine uses the real `PkgReader`, `Pkg.GetEkpfs`, and `PfsReader` APIs. It supports PKGs that the library can open and decrypt; commercial PKGs requiring passcodes or missing keys still require those credentials and are not bypassed. The desktop UI is located in `src/OrbisExtract.App/OrbisExtract.App`; the Avalonia project also includes Android support, but Android validation requires the appropriate SDK, workload, and a physical device or emulator.

Avalonia desktop targets .NET 8 in the current project, while Android targets `net10.0-android`. The Android variant therefore requires the .NET 10 SDK and Android workload. The shared code and backend compile for `net8.0`.

## Build and test

```powershell
dotnet restore OrbisExtract.sln
dotnet build OrbisExtract.sln
dotnet test OrbisExtract.sln --no-restore
```

The desktop application can be built with .NET 8 using `src/OrbisExtract.App/OrbisExtract.App.Desktop/OrbisExtract.App.Desktop.csproj`. Android uses `net10.0-android` and requires the .NET 10 SDK and Android workload.

## Publishing

- Windows: `powershell -ExecutionPolicy Bypass -File scripts/publish-windows.ps1`.
- Linux: `bash scripts/publish-linux.sh` in an environment with the .NET SDK.
- AppImage: run `bash scripts/build-appimage.sh` after publishing Linux; `appimagetool` must be available on `PATH`.
- Android local build: install the .NET 10 SDK, run `dotnet workload install android`, install the Android SDK and JDK 11+, then run `bash scripts/publish-android.sh` in the appropriate environment.
- GitHub Actions: `.github/workflows/android-apk.yml` builds the APK through `workflow_dispatch` or when relevant Android project files change. The APK is uploaded as the `orbisextract-android-apk` artifact for 14 days. The workflow does not automatically install the APK on a phone connected to a developer's computer.

`dotnet publish` does not create an AppImage by itself. The AppImage script builds an AppDir and invokes `appimagetool`. On Android, the SAF picker uses `IStorageFile.OpenReadAsync()` for `content:` URIs and `IStorageFolder` to create output files without converting URIs into physical paths. Persistent permissions/bookmarks depend on the storage provider and have not been validated in the current environment; selections are retained for the current session.

## Security

Internal paths are normalized and checked against path traversal before writing. Each file is written to its own temporary file and committed without silently overwriting existing files. Cancellation and failures remove only temporary files created by the operation.

The project does not create, rebuild, convert, install, download, or manage PKGs.

## Integration testing

No authorized PKG sample is included in this repository. Therefore, real extraction using a legitimate fixture on Windows, Linux, or Android still needs to be performed by the user and is not claimed as validated here.

## License and third-party notices

The main project license is provided in `LICENSE`. Third-party licenses, including OpenOrbis/LibOrbisPkg under LGPL-3.0, are documented in `THIRD-PARTY-NOTICES.md`.
