using System.IO;
using OrbisExtract.App.Localization;

namespace OrbisExtract.App;

public static class AppLicense
{
    private static readonly string FullText = ReadLicense();

    public static string Current => (AppLanguage.IsEnglish
        ? "OrbisExtract is licensed under the GNU General Public License, version 3 only."
        : "O OrbisExtract é distribuído sob a Licença Pública Geral GNU, somente versão 3.")
        + "\n\n" + FullText;

    private static string ReadLicense()
    {
        using var stream = typeof(AppLicense).Assembly.GetManifestResourceStream("OrbisExtract.LICENSE")
            ?? throw new FileNotFoundException("OrbisExtract.LICENSE");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
