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

Builds de teste ficam nos [Artifacts do Actions](https://github.com/flugeru/OrbisExtract/actions/workflows/android-apk.yml), disponíveis por 14 dias. É necessário entrar no GitHub para baixá-los.

| Plataforma | Formato | Artefato de teste |
|---|---|---|
| Windows x64 | Instalador / portátil | `orbisextract-windows-setup` / `orbisextract-windows-exe` |
| Linux x64 | AppImage | `orbisextract-linux-appimage` |
| Android 7.0+ | APK | `OrbisExtract-Android-APK` |

macOS, iOS e navegador não têm distribuição validada. A versão Android ainda não foi publicada na Google Play.

### Novidades da 1.0.5

- **Extração em lote:** jogos independentes são processados em paralelo. Selecione a base e os updates juntos para aplicar as versões em ordem.
- **Leitura em blocos:** buffers reutilizáveis permitem ler e descriptografar o próximo bloco enquanto o atual é gravado.
- **Prévia do jogo:** mostra título, versão e capa do PKG. Quando a imagem está ausente, consulta o Wikimedia Commons e exibe a atribuição.
- **Interface:** seletor de pacotes integrado ao tema, capa com moldura, progresso visível durante a extração e log que ocupa o espaço do cartão.
- **Licença:** GPL-3.0-only no aplicativo e no instalador Windows.

[Notas da versão 1.0.5](docs/release-1.0.5.md)

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

Windows x64, Linux x64 and Android 7.0+ builds are available through GitHub Releases. Test builds are available in [Actions artifacts](https://github.com/flugeru/OrbisExtract/actions/workflows/android-apk.yml) for 14 days and require GitHub sign-in.

Supported distributions: Windows x64 installer/portable, Linux x64 AppImage and Android 7.0+ APK. See the artifact names in the table above. macOS, iOS and browser distributions are not validated. The app is not published on Google Play yet.

### What's new in 1.0.5

- **Batch extraction:** independent games extract in parallel. Select the base package and its updates together to apply versions in order.
- **Buffered extraction:** reusable buffers let the engine read and decrypt the next block while writing the current one.
- **Game preview:** shows the package title, version and artwork. When embedded artwork is unavailable, the app searches Wikimedia Commons and displays attribution.
- **Interface:** themed package picker, framed artwork, progress shown during extraction and a log that fills the card.
- **License:** GPL-3.0-only in the app and Windows installer.

[Version 1.0.5 release notes](docs/release-1.0.5.md)

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
