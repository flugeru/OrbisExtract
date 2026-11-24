# Third-party notices / Avisos de terceiros

**[English](#english)** · **[Português (Brasil)](#português-brasil)**

## English

OrbisExtract's own code is licensed under [GPL-3.0-only](LICENSE). The [historical MIT notice](PRIOR-MIT-LICENSE-NOTICE.md) documents earlier licensing. Third-party components and assets retain their respective licenses and notices.

### Application components

| Component | License and use |
|---|---|
| [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg) | [GNU LGPL v3](vendor/LibOrbisPkg/LICENSE.txt). The vendored C# source, including local changes, is in [`vendor/LibOrbisPkg/`](vendor/LibOrbisPkg/) and is built as a library referenced by `OrbisExtract.Infrastructure.OpenOrbis`. Copyright © 2018–2020 Maxton; credit to the LibOrbisPkg contributors and OpenOrbis. |
| [Avalonia UI](https://github.com/AvaloniaUI/Avalonia) | MIT. UI framework, Fluent theme and platform backends. The [`Avalonia.Fonts.Inter`](https://www.nuget.org/packages/Avalonia.Fonts.Inter) package is MIT; the bundled [Inter font family](https://github.com/rsms/inter/blob/master/LICENSE.txt) is licensed separately under SIL OFL 1.1. |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT. View models and UI state. |
| [Xamarin.AndroidX.Core.SplashScreen](https://www.nuget.org/packages/Xamarin.AndroidX.Core.SplashScreen/1.0.1.15) | `MIT AND Apache-2.0`, as declared by the NuGet package. Android splash screen bindings and AndroidX components. |
| [.NET runtime](https://github.com/dotnet/runtime) | MIT, with [separate third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT). Self-contained desktop builds include runtime components. |

### Development tools and assets

- Tests use [xUnit v2](https://github.com/xunit/xunit) (the referenced 2.5.3 packages and 1.4.0 analyzers declare Apache-2.0), [coverlet](https://github.com/coverlet-coverage/coverlet), [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest), and [MSTest](https://github.com/microsoft/testfx) (MIT). Android diagnostics assets are excluded from Release builds. Inno Setup and `appimagetool` are build tools, not application dependencies.
- Embedded game artwork and game marks shown in screenshots are not covered by the OrbisExtract code license. Artwork retrieved from Wikimedia Commons carries its own attribution and license, shown by the app when available.

This file summarizes the main components. Direct application package versions are declared in [`Directory.Packages.props`](src/OrbisExtract.App/Directory.Packages.props). After restore, each project's `obj/project.assets.json` records the resolved dependencies, including transitive packages. Consult those packages' license and notice files for the target and version you redistribute.

## Português (Brasil)

O código próprio do OrbisExtract é licenciado sob a [GPL-3.0-only](LICENSE). O [aviso MIT histórico](PRIOR-MIT-LICENSE-NOTICE.md) documenta o licenciamento anterior. Componentes e recursos de terceiros mantêm suas respectivas licenças e avisos.

### Componentes do aplicativo

| Componente | Licença e uso |
|---|---|
| [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg) | [GNU LGPL v3](vendor/LibOrbisPkg/LICENSE.txt). O código C#, incluindo as alterações locais, está em [`vendor/LibOrbisPkg/`](vendor/LibOrbisPkg/) e é compilado como biblioteca referenciada por `OrbisExtract.Infrastructure.OpenOrbis`. Copyright © 2018–2020 Maxton; créditos aos contribuidores do LibOrbisPkg e ao OpenOrbis. |
| [Avalonia UI](https://github.com/AvaloniaUI/Avalonia) | MIT. Interface gráfica, tema Fluent e suporte às plataformas. O pacote [`Avalonia.Fonts.Inter`](https://www.nuget.org/packages/Avalonia.Fonts.Inter) usa MIT; a [família tipográfica Inter](https://github.com/rsms/inter/blob/master/LICENSE.txt) incluída no pacote é licenciada separadamente sob SIL OFL 1.1. |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT. Modelos de apresentação e estado da interface. |
| [Xamarin.AndroidX.Core.SplashScreen](https://www.nuget.org/packages/Xamarin.AndroidX.Core.SplashScreen/1.0.1.15) | `MIT AND Apache-2.0`, conforme declarado pelo pacote NuGet. Bindings e componentes AndroidX para a tela de abertura. |
| [Runtime .NET](https://github.com/dotnet/runtime) | MIT, com [avisos de terceiros próprios](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT). Os builds desktop autossuficientes incluem componentes do runtime. |

### Ferramentas de desenvolvimento e recursos

- Os testes usam [xUnit v2](https://github.com/xunit/xunit) (os pacotes 2.5.3 e os analisadores 1.4.0 referenciados declaram Apache-2.0), [coverlet](https://github.com/coverlet-coverage/coverlet), [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) e [MSTest](https://github.com/microsoft/testfx) (MIT). Os recursos de diagnóstico do Android são excluídos dos builds Release. Inno Setup e `appimagetool` são ferramentas de empacotamento, não dependências do aplicativo.
- As capas incluídas nos jogos e as marcas exibidas nas capturas não são cobertas pela licença do código do OrbisExtract. Imagens consultadas no Wikimedia Commons têm atribuição e licença próprias, exibidas no app quando disponíveis.

Este arquivo resume os principais componentes. As versões diretas dos pacotes do aplicativo são declaradas em [`Directory.Packages.props`](src/OrbisExtract.App/Directory.Packages.props). Após a restauração, o `obj/project.assets.json` de cada projeto registra as dependências resolvidas, inclusive as transitivas. Consulte as licenças e os avisos desses pacotes para a plataforma e a versão que você redistribuir.
