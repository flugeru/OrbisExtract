# OrbisExtract

Extrator de pacotes `.PKG` do PlayStation 4, com interface Avalonia e backend C# baseado no commit `92a4e7f8e5914cb06b52ba3ae6b0675da438a051` do [OpenOrbis/LibOrbisPkg](https://github.com/OpenOrbis/LibOrbisPkg).

## Estado atual e limitações

O motor integrado usa as APIs reais `PkgReader`, `Pkg.GetEkpfs` e `PfsReader`. Ele suporta PKGs que a biblioteca consegue abrir e descriptografar; PKGs comerciais que exigem passcode/chaves ainda precisam dessas credenciais e não são contornados. A UI desktop está em `src/OrbisExtract.App/OrbisExtract.App`; o projeto Avalonia multiplataforma gerado inclui Android, mas a validação Android exige SDK/workload e dispositivo ou emulador.

O SDK instalado neste ambiente é .NET 8. Avalonia 12 requer .NET 8+ para desktop e .NET 10 para Android; por isso a variante Android deve ser compilada com .NET 10 e workload Android em uma máquina com esse SDK. O código comum e o backend compilam em `net8.0`.

## Compilar e testar

```powershell
dotnet restore OrbisExtract.sln
dotnet build OrbisExtract.sln
dotnet test OrbisExtract.sln --no-restore
```

O desktop é compilável com .NET 8 pelo projeto `src/OrbisExtract.App/OrbisExtract.App.Desktop/OrbisExtract.App.Desktop.csproj`. O Android permanece em `net10.0-android`, pois Avalonia 12 exige .NET 10 para mobile; esse target requer SDK .NET 10 e workload Android.

## Publicação

- Windows: `powershell -ExecutionPolicy Bypass -File scripts/publish-windows.ps1`.
- Linux: `bash scripts/publish-linux.sh` em ambiente Linux com .NET SDK.
- AppImage: execute `bash scripts/build-appimage.sh` após publicar Linux; requer `appimagetool` no PATH.
- Android local: instale o SDK .NET 10, `dotnet workload install android`, Android SDK e JDK 11+, e execute `bash scripts/publish-android.sh` no ambiente apropriado.
- GitHub Actions: o workflow `.github/workflows/android-apk.yml` compila o APK em `workflow_dispatch` ou quando arquivos Android relevantes são alterados. O APK fica disponível como artifact `orbisextract-android-apk` por 14 dias. O workflow não instala o APK automaticamente em um telefone conectado ao PC.

`dotnet publish` não cria AppImage sozinho. O script monta um AppDir e chama `appimagetool`. No Android, o seletor SAF usa `IStorageFile.OpenReadAsync()` para URIs `content:` e `IStorageFolder` para criar os arquivos de saída sem converter a URI para caminho físico. As permissões/bookmarks persistentes dependem do provedor e ainda não foram validadas neste ambiente; a seleção é mantida durante a sessão atual.

## Segurança

Nomes internos são normalizados e verificados contra traversal antes da escrita. Cada arquivo é escrito em temporário próprio e movido sem sobrescrever arquivos existentes. Cancelamento e falhas removem apenas temporários criados pela operação.

Não há criação, reconstrução, conversão, instalação ou download de PKGs.

## Testes de integração

Não há amostra PKG autorizada incluída neste repositório. Portanto, a extração real com fixture legítima em Windows/Linux/Android ainda precisa ser executada pelo usuário e não é declarada como validada.
