# Avisos de terceiros

## OpenOrbis/LibOrbisPkg

- Repositório: https://github.com/OpenOrbis/LibOrbisPkg
- Commit usado: `92a4e7f8e5914cb06b52ba3ae6b0675da438a051`
- Licença: GNU LGPL-3.0 (`vendor/LibOrbisPkg/LICENSE.txt`)
- Integração: fonte C# referenciada diretamente por `OrbisExtract.Infrastructure.OpenOrbis`; não é executável externo.
- Código correspondente: o diretório `vendor/LibOrbisPkg` contém a fonte do commit utilizado. Alterações locais: nenhuma intencional.

## Avalonia

- Repositório/documentação: https://avaloniaui.net/ e https://github.com/AvaloniaUI/Avalonia
- Versão usada pelo template: 12.1.4.
- Licença: MIT; consulte os arquivos de licença do pacote NuGet restaurado.

## CommunityToolkit.Mvvm

- Repositório: https://github.com/CommunityToolkit/dotnet
- Versão: 8.4.2 (definida pelo template Avalonia).
- Licença: MIT.

As versões efetivas restauradas ficam registradas em `obj/project.assets.json` de cada projeto. Ferramentas de empacotamento (`appimagetool`) não são redistribuídas por este repositório; devem ser obtidas de fonte oficial e verificadas pelo usuário.
