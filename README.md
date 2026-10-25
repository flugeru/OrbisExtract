# OrbisExtract

**English** · **[Português (Brasil)](#português-brasil)**

---

## English

Cross-platform **PlayStation 4 PKG extractor** for dumps made by the user themselves.

The app opens a `.pkg` file, shows its metadata and extracts the files inside to a folder of your choice — nothing more. It does not download, install, create or convert packages, and it contains no keys, passcodes or any protection bypass. It is an extraction tool for your own dumps.

Built in C#/.NET with an [Avalonia](https://avaloniaui.net) UI, using the [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg) library for package reading.

### What it does

- Opens a PS4 `.pkg` file from your device/storage
- Shows package info (title, content, structure)
- Extracts the package contents to a chosen folder, with progress
- Available for **Windows/Linux** (desktop) and **Android** (APK)

### Platform support

| Platform | Status | Build artefact (GitHub Actions) |
|---|---|---|
| **Windows** (x64) | ✅ Supported and tested | `orbisextract-windows-exe` |
| **Linux** (x64) | ✅ Supported — AppImage | `orbisextract-linux-appimage` |
| **Android** (APK) | ✅ Supported and tested | `orbisextract-android-apk` |
| macOS, iOS, Web (Browser) | ❌ Not available | project files exist, but they are not built or validated |

All binaries are produced by the CI workflows on every push and can be downloaded from the [Actions](https://github.com/flugeru/OrbisExtract/actions) page (artefacts are kept for 14 days).

### How to build

**Requirements**

- [.NET SDK 8](https://dotnet.microsoft.com/download) (core, desktop and tests)
- For Android: .NET 10 SDK + `android` workload, Java and Android SDK

**Desktop (Windows/Linux)**

```bash
dotnet restore OrbisExtract.sln
dotnet build OrbisExtract.sln
dotnet test OrbisExtract.sln --no-restore
```

To produce a releasable build:

- Windows: `powershell -ExecutionPolicy Bypass -File scripts/publish-windows.ps1`
- Linux: `bash scripts/publish-linux.sh`
- Linux AppImage: `bash scripts/publish-linux.sh && bash scripts/build-appimage.sh` (requires [`appimagetool`](https://github.com/AppImage/AppImageKit))

**Android**

```bash
bash scripts/publish-android.sh
```

Or download the ready-made APK from the repository's [Actions](https://github.com/flugeru/OrbisExtract) page (workflow `Android APK`, built on every push).

### Credits

- **[OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg)** — library that reads and extracts PKG packages. Source included in `vendor/LibOrbisPkg/` under the **LGPL-3.0** license. Thanks to the OpenOrbis project and the LibOrbisPkg contributors.
- **[Avalonia UI](https://avaloniaui.net)** — cross-platform UI framework (MIT).

Full third-party license notices in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

### License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE).

---

## Português (Brasil)

Aplicativo multiplataforma para **extrair o conteúdo de arquivos `.pkg` de PlayStation 4** a partir de dumps feitos pelo próprio usuário.

Ele abre o pacote, mostra os metadados e extrai os arquivos internos para uma pasta escolhida — nada além disso. Não baixa, não instala, não cria nem converte pacotes, e não contém chaves, passcodes ou qualquer mecanismo de burlar proteção. É uma ferramenta de extração para uso com os seus próprios dumps.

Feito em C#/.NET com interface [Avalonia](https://avaloniaui.net), usando a biblioteca [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg) para a leitura dos pacotes.

### O que ele faz

- Abre um arquivo `.pkg` de PS4 do seu dispositivo/armazenamento
- Exibe as informações do pacote (título, conteúdo, estrutura)
- Extrai os arquivos do pacote para uma pasta de destino, com progresso
- Disponível para **Windows/Linux** (desktop) e **Android** (APK)

### Suporte a plataformas

| Plataforma | Status | Artefato (GitHub Actions) |
|---|---|---|
| **Windows** (x64) | ✅ Suportado e testado | `orbisextract-windows-exe` |
| **Linux** (x64) | ✅ Suportado — AppImage | `orbisextract-linux-appimage` |
| **Android** (APK) | ✅ Suportado e testado | `orbisextract-android-apk` |
| macOS, iOS, Web (Browser) | ❌ Não disponível | os arquivos de projeto existem, mas não são compilados nem validados |

Todos os binários são gerados pelos workflows de CI a cada push e podem ser baixados na página de [Actions](https://github.com/flugeru/OrbisExtract/actions) (os artefatos ficam disponíveis por 14 dias).

### Como compilar

**Requisitos**

- [.NET SDK 8](https://dotnet.microsoft.com/download) (núcleo, desktop e testes)
- Para Android: .NET 10 SDK + workload `android`, Java e Android SDK

**Desktop (Windows/Linux)**

```bash
dotnet restore OrbisExtract.sln
dotnet build OrbisExtract.sln
dotnet test OrbisExtract.sln --no-restore
```

Para gerar a versão publicável:

- Windows: `powershell -ExecutionPolicy Bypass -File scripts/publish-windows.ps1`
- Linux: `bash scripts/publish-linux.sh`
- Linux AppImage: `bash scripts/publish-linux.sh && bash scripts/build-appimage.sh` (requer o [`appimagetool`](https://github.com/AppImage/AppImageKit))

**Android**

```bash
bash scripts/publish-android.sh
```

Ou baixe o APK pronto na página de [Actions](https://github.com/flugeru/OrbisExtract) do repositório (workflow `Android APK`, que compila a cada push).

### Créditos

- **[OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg)** — biblioteca que faz a leitura/extração dos pacotes PKG. A fonte está incluída em `vendor/LibOrbisPkg/` sob licença **LGPL-3.0**. Obrigado ao projeto OpenOrbis e aos contribuintes do LibOrbisPkg.
- **[Avalonia UI](https://avaloniaui.net)** — interface gráfica multiplataforma (MIT).

Avisos completos de licenças de terceiros em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

### Licença

Este projeto está sob a licença **MIT** — veja [LICENSE](LICENSE).
