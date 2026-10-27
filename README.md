# OrbisExtract

[![Release](https://img.shields.io/github/v/release/flugeru/OrbisExtract)](https://github.com/flugeru/OrbisExtract/releases/latest)

**English** · **[Português (Brasil)](#português-brasil)**

---

## English

Cross-platform **PlayStation 4 PKG extractor** for dumps made by the user themselves.

The app opens a `.pkg` file, shows its metadata and extracts the files inside to a folder of your choice — nothing more. It does not download, install, create or convert packages, and it contains no keys, passcodes or any protection bypass. It is an extraction tool for your own dumps.

Built in C#/.NET with an [Avalonia](https://avaloniaui.net) UI, using the [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg) library for package reading.

### Downloads

See the **[1.0.3 release](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.3)**. Once its assets are uploaded, choose the Windows installer or portable ZIP, Linux AppImage, or Android APK.

### Version 1.0.3 changes

- Settings beside About: follow the system language or choose Portuguese or English; manual selection is saved locally.
- Portuguese and English interface, including the main extraction messages and Android notifications.
- Responsive layout with three side-by-side cards on wide desktops; Android keeps a vertical layout.
- More compact About dialog with a persistent Close button and a clickable repository link.
- Bilingual update notes; the no-update prompt now says Close instead of Not now.

The 1.0.3 release page is published; its downloads will appear there after the assets are uploaded. The Play Store AAB is signed locally, not in GitHub Actions.

### What it does

- Opens a PS4 `.pkg` file from your device/storage
- Shows package info (title, content, structure)
- Extracts the package contents to a chosen folder, with progress
- Automatically uses the system language (Portuguese or English), with a saved manual language choice in Settings
- Shows update information in About, with release notes when provided by the update manifest; updates are never installed automatically
- Available for **Windows/Linux** (desktop) and **Android** (APK)

### Platform support

| Platform | Status | Release format |
|---|---|---|
| **Windows** (x64) | ✅ Supported and tested | Installer or portable ZIP |
| **Linux** (x64) | ✅ Supported and tested | AppImage |
| **Android** (APK) | ✅ Supported and tested | APK |
| macOS, iOS, Web (Browser) | ❌ Not available | project files exist, but they are not built or validated |

Downloads: **[1.0.3 release](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.3)** (when assets are available). CI builds of every push are also available as artifacts on the [Actions](https://github.com/flugeru/OrbisExtract/actions) page (kept for 14 days).

### Installing on Windows

Once available, download the Windows installer from the [1.0.3 release](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.3) and run it:

- Installs to `C:\Program Files\OrbisExtract` (Start Menu + desktop shortcut, entry in *Apps and features*)
- **To update an older version:** just run the new installer. It detects the previous installation and installs right over it — same location, no need to uninstall first
- A portable ZIP is also available on releases that include it, for use without installation

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

Or download the Android APK from the [1.0.3 release](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.3), when available. CI also builds an APK on every push as an [Actions](https://github.com/flugeru/OrbisExtract/actions) artifact.

For a Google Play upload, see the [local signed AAB instructions](docs/android-play-upload-local.md). Your upload key stays on your computer; the GitHub Actions build continues to provide an installable APK only.

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

Veja o **[release 1.0.3](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.3)**. Quando os arquivos forem enviados, escolha o instalador ou ZIP portátil do Windows, AppImage do Linux ou APK do Android.

### Mudanças da versão 1.0.3

- Configurações ao lado de Sobre: seguir o idioma do sistema ou escolher português ou inglês; a escolha manual é salva localmente.
- Interface em português e inglês, incluindo as principais mensagens de extração e notificações Android.
- Layout responsivo com três cartões lado a lado em desktops largos; no Android, a disposição continua vertical.
- Janela Sobre mais compacta, com botão Fechar fixo e link clicável para o repositório.
- Notas de atualização bilíngues; o aviso de que não há atualização agora mostra Fechar em vez de Agora não.

A página do release 1.0.3 já está publicada; os downloads aparecerão nela após o envio dos arquivos. O AAB da Play Store é assinado localmente, não no GitHub Actions.

### O que ele faz

- Abre um arquivo `.pkg` de PS4 do seu dispositivo/armazenamento
- Exibe as informações do pacote (título, conteúdo, estrutura)
- Extrai os arquivos do pacote para uma pasta de destino, com progresso
- Detecta o idioma do sistema (português ou inglês) e permite salvar uma escolha manual em Configurações
- Exibe informações de atualização em Sobre, com notas quando disponíveis; não instala atualizações automaticamente
- Disponível para **Windows/Linux** (desktop) e **Android** (APK)

### Suporte a plataformas

| Plataforma | Status | Formato do release |
|---|---|---|
| **Windows** (x64) | ✅ Suportado e testado | Instalador ou ZIP portátil |
| **Linux** (x64) | ✅ Suportado e testado | AppImage |
| **Android** (APK) | ✅ Suportado e testado | APK |
| macOS, iOS, Web (Browser) | ❌ Não disponível | os arquivos de projeto existem, mas não são compilados nem validados |

Downloads: **[release 1.0.3](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.3)** (quando os arquivos estiverem disponíveis). Os builds de cada push também ficam disponíveis como artefatos na página de [Actions](https://github.com/flugeru/OrbisExtract/actions) (por 14 dias).

### Instalação no Windows

Quando disponível, baixe o instalador Windows no [release 1.0.3](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.3) e execute:

- Instala em `C:\Program Files\OrbisExtract` (atalhos no Menu Iniciar e na área de trabalho, entrada em *Programas e Recursos*)
- **Para atualizar uma versão antiga:** basta rodar o instalador novo. Ele detecta a instalação anterior e instala por cima — mesma pasta, sem precisar desinstalar antes
- Se o release incluir o ZIP portátil, ele pode ser usado sem instalação

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

Ou baixe o APK do Android no [release 1.0.3](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.3), quando disponível. O CI também gera um APK a cada push como artefato de [Actions](https://github.com/flugeru/OrbisExtract/actions).

Para publicar na Google Play, veja as [instruções do AAB assinado localmente](docs/android-play-upload-local.md). Sua chave de upload permanece no seu computador; o GitHub Actions continua gerando somente o APK instalável.

### Créditos

- **[OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg)** — biblioteca que faz a leitura/extração dos pacotes PKG. A fonte está incluída em `vendor/LibOrbisPkg/` sob licença **LGPL-3.0**. Obrigado ao projeto OpenOrbis e aos contribuintes do LibOrbisPkg.
- **[Avalonia UI](https://avaloniaui.net)** — interface gráfica multiplataforma (MIT).

Avisos completos de licenças de terceiros em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

### Licença

Este projeto está sob a licença **MIT** — veja [LICENSE](LICENSE).
