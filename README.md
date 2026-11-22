<div align="center">

<img src="OrbisExtract-transparent.png" alt="Logo do OrbisExtract" width="128" />

# OrbisExtract

[![Release](https://img.shields.io/github/v/release/flugeru/OrbisExtract)](https://github.com/flugeru/OrbisExtract/releases/latest)

</div>

**[Português (Brasil)](#português-brasil)** · **[English](#english)**

## Português (Brasil)

Extrator de arquivos **PKG de PlayStation 4** para Windows, Linux e Android, feito em C#/.NET com Avalonia e OpenOrbis/LibOrbisPkg. Abre o pacote, mostra metadados e extrai seu conteúdo para uma pasta escolhida.

Use com seus próprios dumps. O aplicativo não baixa, instala, cria nem converte pacotes. Pacotes protegidos podem não ser compatíveis; não há campo de passcode na interface.

### Downloads

**[Última versão publicada](https://github.com/flugeru/OrbisExtract/releases/latest)** · **[Releases](https://github.com/flugeru/OrbisExtract/releases)**

**[Versão 1.0.4 disponível](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.4):** [Windows instalador](https://github.com/flugeru/OrbisExtract/releases/download/1.0.4/OrbisExtract-1.0.4-windows-x64-setup.exe) · [Windows portátil](https://github.com/flugeru/OrbisExtract/releases/download/1.0.4/OrbisExtract-1.0.4-windows-x64-portable.zip) · [Linux AppImage](https://github.com/flugeru/OrbisExtract/releases/download/1.0.4/OrbisExtract-1.0.4-x86_64.AppImage) · [Android APK](https://github.com/flugeru/OrbisExtract/releases/download/1.0.4/OrbisExtract-1.0.4-signed.apk).

Builds de teste ficam nos [Artifacts do Actions](https://github.com/flugeru/OrbisExtract/actions/workflows/android-apk.yml), disponíveis por 14 dias. É necessário entrar no GitHub para baixá-los.

| Plataforma | Formato | Artefato de teste |
|---|---|---|
| Windows x64 | Instalador / portátil | `orbisextract-windows-setup` / `orbisextract-windows-exe` |
| Linux x64 | AppImage | `orbisextract-linux-appimage` |
| Android 7.0+ | APK | `OrbisExtract-Android-APK` |

macOS, iOS e navegador não têm distribuição validada. A versão Android ainda não foi publicada na Google Play.

### Novidades da 1.0.4

- Extração otimizada: menos chamadas AES, processamento XTS em lotes, leitura Android com buffer e reutilização de pastas do provedor de armazenamento.
- Pausar, retomar e cancelar a extração; controles também na notificação Android, com ajustes para HyperOS.
- Histórico local de extrações com resultado, duração e relatório detalhado.
- Barra de título personalizada no desktop, sem faixa cinza superior no Windows, mantendo estilos usados pelo Snap e redimensionamento.
- Instalador Windows escuro, com identidade visual e licença GPL-3.0.
- Ajustes de layout, traduções e diagnósticos de desempenho no log.
- APKs do Actions com chave persistente em GitHub Actions Secrets e código de versão crescente.

No teste informado pelo usuário, o mesmo PKG passou de **03:29 para cerca de 44 s no POCO X6 Pro**, usando memória interna. É uma medição desse aparelho e pacote, não uma garantia para outros dispositivos.

### Atualização e uso

1. Instale a versão correspondente à plataforma. No Windows, execute o instalador novo sobre a instalação anterior; o portátil dispensa instalação.
2. Selecione um ou mais PKGs e a pasta de destino. Para integrar updates, inclua o jogo base na mesma seleção. Confira o espaço livre, pois a extração pode ocupar mais espaço que o PKG.
3. Inicie a extração. Use os controles de pausa/cancelamento e consulte o histórico ou o log em caso de erro.

No modo em lote, jogos independentes são extraídos em paralelo. Cada grupo usa uma subpasta própria, publicada após a extração da base e a aplicação dos updates em ordem de `APP_VER`. Pastas de destino existentes são preservadas. Updates delta binários exigem reconstrução e ficam fora dessa integração. No Android, selecione uma pasta em um provedor que permita renomear pastas, como o armazenamento interno. A prévia usa `icon0.png` ou `pic1.png` do próprio PKG quando disponível.

O limite de extrações simultâneas considera CPU, memória disponível, tamanho das imagens PFS e tipo de armazenamento. Para arquivos de 8 MiB ou mais, o motor pode ler e descriptografar o próximo bloco enquanto grava o atual, usando dois buffers reutilizáveis de 1 MiB por extração ativa.

Se o PKG não tiver arte utilizável, a prévia busca uma capa ou captura de tela no Wikimedia Commons em segundo plano. A consulta envia o título do jogo, sem enviar o arquivo, seu caminho ou seu identificador. Uma imagem encontrada mostra a autoria, a fonte e a licença. A seleção e a extração funcionam mesmo sem conexão.

**Android:** instalar por cima exige o mesmo identificador e assinatura compatível, sem reduzir o código de versão. Builds novos usam a chave persistente do projeto; APKs antigos assinados com outras chaves podem exigir reinstalação. Preserve os dados importantes antes de desinstalar. A chave privada não fica no repositório nem nos artefatos; os dados do certificado público acompanham o APK do Actions.

Português e inglês disponíveis em Configurações, com detecção automática do sistema ou escolha manual salva. Atualizações são consultadas pelo aplicativo, mas nunca instaladas automaticamente.

### Compilar

Desktop e testes: **.NET SDK 8**. Android: **.NET SDK 10**, workload `android`, Java 17 e Android SDK 36.

```sh
dotnet restore OrbisExtract.sln
dotnet build OrbisExtract.sln -c Release
dotnet test OrbisExtract.sln -c Release --no-restore
python tests/check_android_updates.py
```

| Saída | Comando |
|---|---|
| Windows | `powershell -ExecutionPolicy Bypass -File scripts/publish-windows.ps1` |
| Instalador, após publicar Windows | `ISCC packaging\windows\orbisextract.iss` — Inno Setup 6 atualizado |
| Linux | `bash scripts/publish-linux.sh` |
| AppImage, após publicar Linux | `bash scripts/build-appimage.sh` — requer `appimagetool` |
| Android local | `bash scripts/publish-android.sh` |

No CI, `ANDROID_SIGNING_KEYSTORE_BASE64` deve conter o keystore existente em Base64; o build falha se o Secret estiver ausente, em vez de gerar outra chave. Não use uma chave diferente se precisar manter compatibilidade com instalações anteriores. A assinatura local precisa ser configurada com a mesma chave para produzir APKs compatíveis com os do CI.

## English

Cross-platform **PlayStation 4 PKG extractor**, built with C#/.NET, Avalonia and OpenOrbis/LibOrbisPkg. Open a package, inspect its metadata and extract its contents to a selected folder.

Use your own dumps. The app does not download, install, create or convert packages. Protected packages may not be supported; the UI has no passcode field.

### Downloads

**[Latest published release](https://github.com/flugeru/OrbisExtract/releases/latest)** · **[All releases](https://github.com/flugeru/OrbisExtract/releases)**

**[Version 1.0.4 is available](https://github.com/flugeru/OrbisExtract/releases/tag/1.0.4).** Download the Windows installer/portable, Linux AppImage or Android APK using the links above. Test builds remain available as [Actions artifacts](https://github.com/flugeru/OrbisExtract/actions/workflows/android-apk.yml) for 14 days; GitHub sign-in is required for artifacts, not release downloads.

Supported distributions: Windows x64 installer/portable, Linux x64 AppImage and Android 7.0+ APK. See the artifact names in the table above. macOS, iOS and browser distributions are not validated. The app is not published on Google Play yet.

### What's new in 1.0.4

- Faster extraction through batched AES-XTS processing, buffered Android input and reused storage-provider folders.
- Pause, resume and cancel, including Android notification controls with HyperOS layout fixes.
- Local extraction history with outcomes, durations and detailed reports.
- Custom desktop title bar without the gray Windows top strip, retaining native styles used for Snap and resizing.
- Dark branded Windows installer with the GPL-3.0 license.
- Layout and translation fixes, plus extraction performance diagnostics.
- Actions APKs signed with a persistent GitHub Actions Secret and an increasing version code.

A user-reported test of the same PKG improved from **03:29 to about 44 seconds on a POCO X6 Pro**, using internal storage. Results depend on the device, package and storage; this is not a general benchmark guarantee.

### Updating and extracting

Run the new Windows installer over the previous installation, or use the portable build. Select one or more PKGs and an output folder with enough free space. To apply updates, include the base game in the same selection. Independent groups extract concurrently into separate folders. Each folder becomes final after its base and ordered `APP_VER` updates complete. Binary delta updates require reconstruction. On Android, use a storage provider that can rename folders, such as internal storage. The app previews embedded `icon0.png` or `pic1.png` artwork when available.

**Android updates require a compatible signing identity, the same package ID and no version-code downgrade.** New CI builds reuse the project signing key; older APKs with different signatures may require reinstallation. Preserve important app data before uninstalling. Private signing material is never included in the repository or download artifacts; public certificate details accompany the Actions APK.

Settings support system language detection or a saved Portuguese/English selection. The app checks for updates but never installs them automatically.

### Building

Use .NET SDK 8 for desktop/tests; Android requires .NET SDK 10, the `android` workload, Java 17 and Android SDK 36. Build/test commands and platform publishing scripts are listed above. CI requires the existing Base64 keystore in `ANDROID_SIGNING_KEYSTORE_BASE64`; missing signing material fails the build rather than silently replacing the key. Local builds need the same signing key to update CI installations.

## Credits / Créditos

- [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg): package-reading library, included with local changes in `vendor/LibOrbisPkg/`, LGPL-3.0.
- [Avalonia UI](https://avaloniaui.net): cross-platform UI, MIT.

[Third-party notices](THIRD-PARTY-NOTICES.md) · [Project license: GPL-3.0-only](LICENSE) · [Prior MIT notice](PRIOR-MIT-LICENSE-NOTICE.md)
