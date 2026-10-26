# OrbisExtract

[![Release](https://img.shields.io/github/v/release/flugeru/OrbisExtract)](https://github.com/flugeru/OrbisExtract/releases/latest)

**English** · **[Português (Brasil)](#português-brasil)**

---

## English

Cross-platform **PlayStation 4 PKG extractor** for dumps made by the user themselves.

The app opens a `.pkg` file, shows its metadata and extracts the files inside to a folder of your choice — nothing more. It does not download, install, create or convert packages, and it contains no keys, passcodes or any protection bypass. It is an extraction tool for your own dumps.

Built in C#/.NET with an [Avalonia](https://avaloniaui.net) UI, using the [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg) library for package reading.

### Downloads

Download **1.0.1** from the **[release page](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.1)**:

| Platform | Release asset |
|---|---|
| Windows x64 — installer | [`OrbisExtract-1.0.1-windows-x64-setup.exe`](https://github.com/flugeru/OrbisExtract/releases/download/1.0.1/OrbisExtract-1.0.1-windows-x64-setup.exe) |
| Windows x64 — portable | [`OrbisExtract-1.0.1-windows-x64-portable.zip`](https://github.com/flugeru/OrbisExtract/releases/download/1.0.1/OrbisExtract-1.0.1-windows-x64-portable.zip) |
| Linux x64 — AppImage | [`OrbisExtract-1.0.1-x86_64.appimage`](https://github.com/flugeru/OrbisExtract/releases/download/1.0.1/OrbisExtract-1.0.1-x86_64.appimage) |
| Android — APK | [`OrbisExtract-1.0.1-signed.apk`](https://github.com/flugeru/OrbisExtract/releases/download/1.0.1/OrbisExtract-1.0.1-signed.apk) |

### What it does

- Opens a PS4 `.pkg` file from your device/storage
- Shows package info (title, content, structure)
- Extracts the package contents to a chosen folder, with progress
- Available for **Windows/Linux** (desktop) and **Android** (APK)

### Platform support

| Platform | Status | Release asset (1.0.1) |
|---|---|---|
| **Windows** (x64) | ✅ Supported and tested | `OrbisExtract-1.0.1-windows-x64-setup.exe` (installer) · `OrbisExtract-1.0.1-windows-x64-portable.zip` (portable) |
| **Linux** (x64) | ✅ Supported and tested | `OrbisExtract-1.0.1-x86_64.appimage` |
| **Android** (APK) | ✅ Supported and tested | `OrbisExtract-1.0.1-signed.apk` |
| macOS, iOS, Web (Browser) | ❌ Not available | project files exist, but they are not built or validated |

Downloads: **[latest release](https://github.com/flugeru/OrbisExtract/releases/latest)**. CI builds of every push are also available as artifacts on the [Actions](https://github.com/flugeru/OrbisExtract/actions) page (kept for 14 days).

### Installing on Windows

Download `OrbisExtract-1.0.1-windows-x64-setup.exe` from the [1.0.1 release](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.1) and run it:

- Installs to `C:\Program Files\OrbisExtract` (Start Menu + desktop shortcut, entry in *Apps and features*)
- **To update an older version:** just run the new installer. It detects the previous installation and installs right over it — same location, no need to uninstall first
- The portable `OrbisExtract-1.0.1-windows-x64-portable.zip` remains available without installation

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
- Windows installer: after publishing, run `ISCC packaging\windows\orbisextract.iss` (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php))
- Linux: `bash scripts/publish-linux.sh`
- Linux AppImage: `bash scripts/publish-linux.sh && bash scripts/build-appimage.sh` (requires [`appimagetool`](https://github.com/AppImage/AppImageKit))

**Android**

```bash
bash scripts/publish-android.sh
```

Or download `OrbisExtract-1.0.1-signed.apk` from the [1.0.1 release](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.1). CI also builds an APK on every push as an [Actions](https://github.com/flugeru/OrbisExtract/actions) artifact.

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

### Downloads

Baixe a versão **1.0.1** na **[página do release](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.1)**:

| Plataforma | Arquivo do release |
|---|---|
| Windows x64 — instalador | [`OrbisExtract-1.0.1-windows-x64-setup.exe`](https://github.com/flugeru/OrbisExtract/releases/download/1.0.1/OrbisExtract-1.0.1-windows-x64-setup.exe) |
| Windows x64 — portátil | [`OrbisExtract-1.0.1-windows-x64-portable.zip`](https://github.com/flugeru/OrbisExtract/releases/download/1.0.1/OrbisExtract-1.0.1-windows-x64-portable.zip) |
| Linux x64 — AppImage | [`OrbisExtract-1.0.1-x86_64.appimage`](https://github.com/flugeru/OrbisExtract/releases/download/1.0.1/OrbisExtract-1.0.1-x86_64.appimage) |
| Android — APK | [`OrbisExtract-1.0.1-signed.apk`](https://github.com/flugeru/OrbisExtract/releases/download/1.0.1/OrbisExtract-1.0.1-signed.apk) |

### O que ele faz

- Abre um arquivo `.pkg` de PS4 do seu dispositivo/armazenamento
- Exibe as informações do pacote (título, conteúdo, estrutura)
- Extrai os arquivos do pacote para uma pasta de destino, com progresso
- Disponível para **Windows/Linux** (desktop) e **Android** (APK)

### Suporte a plataformas

| Plataforma | Status | Arquivo do release (1.0.1) |
|---|---|---|
| **Windows** (x64) | ✅ Suportado e testado | `OrbisExtract-1.0.1-windows-x64-setup.exe` (instalador) · `OrbisExtract-1.0.1-windows-x64-portable.zip` (portátil) |
| **Linux** (x64) | ✅ Suportado e testado | `OrbisExtract-1.0.1-x86_64.appimage` |
| **Android** (APK) | ✅ Suportado e testado | `OrbisExtract-1.0.1-signed.apk` |
| macOS, iOS, Web (Browser) | ❌ Não disponível | os arquivos de projeto existem, mas não são compilados nem validados |

Downloads: **[último release](https://github.com/flugeru/OrbisExtract/releases/latest)**. Os builds de cada push também ficam disponíveis como artefatos na página de [Actions](https://github.com/flugeru/OrbisExtract/actions) (por 14 dias).

### Instalação no Windows

Baixe `OrbisExtract-1.0.1-windows-x64-setup.exe` no [release 1.0.1](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.1) e execute:

- Instala em `C:\Program Files\OrbisExtract` (atalhos no Menu Iniciar e na área de trabalho, entrada em *Programas e Recursos*)
- **Para atualizar uma versão antiga:** basta rodar o instalador novo. Ele detecta a instalação anterior e instala por cima — mesma pasta, sem precisar desinstalar antes
- O portátil `OrbisExtract-1.0.1-windows-x64-portable.zip` continua disponível para quem prefere sem instalação

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
- Instalador Windows: após o publish, rode `ISCC packaging\windows\orbisextract.iss` (requer o [Inno Setup 6](https://jrsoftware.org/isinfo.php))
- Linux: `bash scripts/publish-linux.sh`
- Linux AppImage: `bash scripts/publish-linux.sh && bash scripts/build-appimage.sh` (requer o [`appimagetool`](https://github.com/AppImage/AppImageKit))

**Android**

```bash
bash scripts/publish-android.sh
```

Ou baixe `OrbisExtract-1.0.1-signed.apk` no [release 1.0.1](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.1). O CI também gera um APK a cada push como artefato de [Actions](https://github.com/flugeru/OrbisExtract/actions).

### Créditos

- **[OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg)** — biblioteca que faz a leitura/extração dos pacotes PKG. A fonte está incluída em `vendor/LibOrbisPkg/` sob licença **LGPL-3.0**. Obrigado ao projeto OpenOrbis e aos contribuintes do LibOrbisPkg.
- **[Avalonia UI](https://avaloniaui.net)** — interface gráfica multiplataforma (MIT).

Avisos completos de licenças de terceiros em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

### Licença

Este projeto está sob a licença **MIT** — veja [LICENSE](LICENSE).
