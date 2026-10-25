# Avisos de terceiros

Componentes usados pelo OrbisExtract e suas respectivas licenças. O código próprio deste repositório está sob a licença MIT (ver `LICENSE`).

## OpenOrbis / LibOrbisPkg

- **Projeto:** [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg)
- **Licença:** LGPL-3.0 (`vendor/LibOrbisPkg/LICENSE.txt`)
- **Como é usado:** a fonte C# está incluída neste repositório em `vendor/LibOrbisPkg/` e é compilada junto com o aplicativo pelo projeto `OrbisExtract.Infrastructure.OpenOrbis`. Não é um executável separado.
- **Código-fonte:** o diretório `vendor/LibOrbisPkg/` contém a fonte correspondente, disponível para verificação e substituição conforme exigido pela LGPL-3.0.

Créditos aos autores e contribuintes do LibOrbisPkg e ao projeto [OpenOrbis](https://github.com/OpenOrbis).

## Avalonia UI

- **Projeto:** [AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia) — https://avaloniaui.net
- **Licença:** MIT
- **Como é usado:** pacotes NuGet `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.Android` e afins (interface gráfica).

## CommunityToolkit.Mvvm

- **Projeto:** [CommunityToolkit/dotnet](https://github.com/CommunityToolkit/dotnet)
- **Licença:** MIT
- **Como é usado:** pacote NuGet `CommunityToolkit.Mvvm` (padrão MVVM da interface).

## Xamarin.AndroidX.Core.SplashScreen

- **Projeto:** [dotnet/android](https://github.com/dotnet/android) / AndroidX
- **Licença:** Apache-2.0
- **Como é usado:** pacote NuGet no projeto Android (splash screen).

## .NET

- **Projeto:** [.NET](https://dotnet.microsoft.com)
- **Licença:** MIT
- **Como é usado:** runtime e SDK que executam o aplicativo.

## Ferramentas de teste (somente desenvolvimento)

- [xUnit](https://xunit.net) — MIT
- [coverlet](https://github.com/coverlet-coverage/coverlet) — MIT
- [MSTest](https://github.com/microsoft/testfx) — MIT (usado pelos testes do LibOrbisPkg em `vendor/`)

Não são distribuídos junto com o aplicativo.

---

As versões exatas restauradas ficam registradas em `obj/project.assets.json` de cada projeto após o `dotnet restore`. Ferramentas de empacotamento (como `appimagetool`) não são redistribuídas por este repositório.
