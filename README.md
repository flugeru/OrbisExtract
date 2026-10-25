# OrbisExtract

Aplicativo multiplataforma para **extrair o conteúdo de arquivos `.pkg` de PlayStation 4** a partir de dumps feitos pelo próprio usuário.

Ele lê o pacote, mostra os metadados e extrai os arquivos internos para uma pasta escolhida — nada além disso. Não baixa, não instala, não cria nem converte pacotes, e não contém chaves, passcodes ou qualquer mecanismo de burlar proteção. É uma ferramenta de extração para uso com os seus próprios dumps.

Feito em C#/.NET com interface [Avalonia](https://avaloniaui.net), usando a biblioteca [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg) para a leitura dos pacotes.

## O que ele faz

- Abre um arquivo `.pkg` de PS4 do seu dispositivo/armazenamento
- Exibe as informações do pacote (título, conteúdo, estrutura)
- Extrai os arquivos do pacote para uma pasta de destino, com progresso
- Disponível para **Windows/Linux** (desktop) e **Android** (APK)

## Como compilar

### Requisitos

- [.NET SDK 8](https://dotnet.microsoft.com/download) (núcleo, desktop e testes)
- Para Android: .NET 10 SDK + workload `android`, Java e Android SDK

### Desktop (Windows/Linux)

```bash
dotnet restore OrbisExtract.sln
dotnet build OrbisExtract.sln
dotnet test OrbisExtract.sln --no-restore
```

Para gerar a versão publicável:

- Windows: `powershell -ExecutionPolicy Bypass -File scripts/publish-windows.ps1`
- Linux: `bash scripts/publish-linux.sh`

### Android

```bash
bash scripts/publish-android.sh
```

Ou baixe o APK pronto na página de [Actions](https://github.com/flugeru/OrbisExtract/actions) do repositório (workflow `Android APK`, que compila no push).

## Créditos

- **[OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg)** — biblioteca que faz a leitura/extração dos pacotes PKG. A fonte está incluída em `vendor/LibOrbisPkg/` sob licença **LGPL-3.0**. Obrigado ao projeto OpenOrbis e aos contribuintes do LibOrbisPkg.
- **[Avalonia UI](https://avaloniaui.net)** — interface gráfica multiplataforma (MIT).

Avisos completos de licenças de terceiros em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licença

Este projeto está sob a licença **MIT** — veja [LICENSE](LICENSE).
