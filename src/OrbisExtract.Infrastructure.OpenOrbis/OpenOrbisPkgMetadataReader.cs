using LibOrbisPkg.PKG;
using OrbisExtract.Core;

namespace OrbisExtract.Infrastructure.OpenOrbis;

public sealed class OpenOrbisPkgMetadataReader : IPkgMetadataReader
{
    public async Task<PackageMetadata> ReadAsync(Stream package, CancellationToken cancellationToken = default)
    {
        if (!package.CanSeek)
            throw new PkgExtractionException("A leitura de metadados exige uma origem pesquisável.");

        var pkg = await Task.Run(() => new PkgReader(package).ReadPkg(), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        static string ReadValue(Pkg pkg, string key, string fallback)
        {
            var value = pkg.ParamSfo?.ParamSfo?[key];
            return value?.ToString()?.TrimEnd('\0') ?? fallback;
        }

        return new PackageMetadata(
            ReadValue(pkg, "TITLE", "Título não informado"),
            ReadValue(pkg, "VERSION", ReadValue(pkg, "APP_VER", "Versão não informada")),
            pkg.Header.content_id ?? "Content ID não informado");
    }
}
