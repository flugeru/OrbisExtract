using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OrbisExtract.Core;
using OrbisExtract.Infrastructure.OpenOrbis;
using System.Collections.Generic;
using Button = Avalonia.Controls.Button;
using ProgressBar = Avalonia.Controls.ProgressBar;

namespace OrbisExtract.App.Views;

public sealed class MainView : UserControl
{
    private readonly TextBlock _fileName;
    private readonly TextBlock _metadata;
    private readonly TextBlock _status;
    private readonly TextBox _log;
    private readonly ProgressBar _progress;
    private readonly TextBox _passcode;
    private readonly Button _choosePackage;
    private readonly Button _chooseDestination;
    private readonly Button _extract;
    private IStorageFile? _package;
    private IStorageFolder? _destination;
    private readonly CancellationTokenSource _lifetime = new();

    public MainView()
    {
        Background = new SolidColorBrush(Color.Parse("#0B0D12"));
        Foreground = Brushes.White;

        _fileName = Label("Nenhum pacote selecionado", 14, "#A8B0C0");
        _metadata = Label("Selecione um PKG para consultar os metadados.", 13, "#A8B0C0");
        _status = Label("Pronto para começar.", 13, "#C9D1DF");
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 7, IsVisible = false };
        _log = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 150,
            MaxHeight = 280,
            FontSize = 11,
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Color.Parse("#0D1017")),
            Foreground = new SolidColorBrush(Color.Parse("#C9D1DF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#272E3B")),
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Text = $"{DateTime.Now:HH:mm:ss}  [INFO]  OrbisExtract iniciado."
        };
        _passcode = new TextBox
        {
            PlaceholderText = "Passcode (opcional)",
            PasswordChar = '•',
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Color.Parse("#171B24")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#343B49"))
        };

        _choosePackage = ActionButton("Abrir arquivo PKG", "#6759E8");
        _choosePackage.Click += async (_, _) => await ChoosePackageAsync();
        _chooseDestination = ActionButton("Pasta de destino", "#222834");
        _chooseDestination.Click += async (_, _) => await ChooseDestinationAsync();
        _extract = ActionButton("Extrair arquivos", "#6759E8");
        _extract.IsEnabled = false;
        _extract.Click += async (_, _) => await ExtractAsync();

        var card = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#121620")),
            BorderBrush = new SolidColorBrush(Color.Parse("#272E3B")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    Label("PACOTE SELECIONADO", 11, "#8791A4"),
                    _fileName,
                    _metadata,
                    _choosePackage,
                    new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#272E3B")) },
                    Label("DESTINO DA EXTRAÇÃO", 11, "#8791A4"),
                    _chooseDestination,
                    Label("CHAVE / PASSCODE", 11, "#8791A4"),
                    _passcode,
                    _extract,
                    _progress,
                    Label("LOG DA EXTRAÇÃO", 11, "#8791A4"),
                    _log,
                    _status
                }
            }
        };

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 22,
                Margin = new Thickness(22, 28),
                MaxWidth = 720,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    Label("ORBIS", 12, "#958AFF"),
                    new TextBlock
                    {
                        Text = "OrbisExtract",
                        FontSize = 34,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "PlayStation 4 · PKG Extractor",
                        FontSize = 15,
                        Foreground = new SolidColorBrush(Color.Parse("#A8B0C0")),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    card,
                    new TextBlock
                    {
                        Text = "Os resultados dependem do formato do pacote e das chaves/passcodes legítimos necessários.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#788294")),
                        FontSize = 12
                    }
                }
            }
        };
    }

    private void Log(string message)
    {
        _log.Text += Environment.NewLine + $"{DateTime.Now:HH:mm:ss.fff}  {message}";
        _log.CaretIndex = _log.Text.Length;
    }

    private static TextBlock Label(string text, double size, string color) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = new SolidColorBrush(Color.Parse(color)),
        TextWrapping = TextWrapping.Wrap
    };

    private static Button ActionButton(string text, string color) => new()
    {
        Content = text,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        Padding = new Thickness(14, 13),
        Background = new SolidColorBrush(Color.Parse(color)),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(9),
        FontSize = 14
    };

    private async Task ChoosePackageAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar pacote PlayStation PKG",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Pacotes PKG") { Patterns = ["*.pkg", "*.PKG"] },
                FilePickerFileTypes.All
            ]
        });
        if (files.Count == 0) return;

        _package = files[0];
        _fileName.Text = _package.Name;
        _metadata.Text = "Lendo metadados do pacote…";
        _status.Text = "Arquivo selecionado.";
        Log($"Pacote selecionado: {_package.Name}");
        _extract.IsEnabled = false;

        try
        {
            await using var stream = await _package.OpenReadAsync();
            if (!stream.CanSeek)
            {
                _metadata.Text = "Metadados serão validados durante a extração.";
            }
            else
            {
                Log("Lendo metadados do PKG…");
                var info = await new OpenOrbisPkgMetadataReader().ReadAsync(stream, _lifetime.Token);
                Log($"Metadados lidos: título={info.Title}; versão={info.Version}; content ID={info.ContentId}");
                _metadata.Text = $"{info.Title}\nVersão: {info.Version}\nContent ID: {info.ContentId}";
            }
        }
        catch (Exception ex)
        {
            _metadata.Text = "Metadados indisponíveis; o pacote ainda pode ser tentado.";
            _status.Text = ex.Message;
            Log($"Aviso ao ler metadados: {ex}");
        }
        _extract.IsEnabled = _destination is not null;
    }

    private async Task ChooseDestinationAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Escolher pasta para os arquivos extraídos",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        _destination = folders[0];
        _chooseDestination.Content = $"Destino: {_destination.Name}";
        _extract.IsEnabled = _package is not null;
        _status.Text = "Destino selecionado.";
        Log($"Destino selecionado: {_destination.Name}");
    }

    private async Task ExtractAsync()
    {
        if (_package is null || _destination is null) return;
        _choosePackage.IsEnabled = _chooseDestination.IsEnabled = _extract.IsEnabled = false;
        _progress.IsVisible = true;
        _progress.Value = 0;
        _status.Text = "Preparando a extração…";
        Log("--------------------------------------------------");
        Log($"Iniciando extração. Pacote: {_package.Name}");
        Log($"Destino: {_destination.Name}");
        Log($"Passcode informado: {!string.IsNullOrWhiteSpace(_passcode.Text)}");
        string? tempPath = null;
        try
        {
            // Android content:// streams are not guaranteed to support seeking. Copy to
            // an app-private temporary file so LibOrbisPkg can read the PKG safely.
            tempPath = Path.Combine(Path.GetTempPath(), $"orbisextract-{Guid.NewGuid():N}.pkg");
            Log("Copiando o PKG para um arquivo temporário pesquisável…");
            await using (var source = await _package.OpenReadAsync())
            await using (var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024, true))
                await source.CopyToAsync(temp, _lifetime.Token);
            Log($"Cópia temporária concluída. Tamanho: {new FileInfo(tempPath).Length:N0} bytes.");

            await using var packageStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true);
            Log("Abrindo e validando a estrutura interna do PKG…");
            var destination = new StorageFolderExtractionDestination(_destination);
            var progress = new Progress<ExtractionProgress>(p =>
            {
                if (p.FilesTotal is > 0) _progress.Value = Math.Min(100, p.FilesCompleted * 100d / p.FilesTotal.Value);
                _status.Text = p.CurrentPath is null
                    ? $"{p.State}: {p.FilesCompleted:N0} arquivos"
                    : $"{p.FilesCompleted:N0} arquivo(s) · {p.CurrentPath}";
                if (p.CurrentPath is not null) Log($"[{p.FilesCompleted:N0}/{(p.FilesTotal?.ToString("N0") ?? "?")}] {p.CurrentPath} ({p.BytesCompleted:N0} bytes processados)");
                else Log($"Estado: {p.State}; arquivos={p.FilesCompleted:N0}; bytes={p.BytesCompleted:N0}");
            });
            var result = await new OpenOrbisPkgExtractor().ExtractAsync(
                new ExtractionRequest(packageStream, string.Empty, string.IsNullOrWhiteSpace(_passcode.Text) ? null : _passcode.Text, Destination: destination),
                progress,
                _lifetime.Token);
            _progress.Value = 100;
            Log($"Extração concluída. Arquivos: {result.FilesExtracted:N0}; bytes: {result.BytesExtracted:N0}.");
            _status.Text = $"Concluído: {result.FilesExtracted:N0} arquivos · {result.BytesExtracted / 1024d / 1024d:N1} MiB.";
        }
        catch (OperationCanceledException ex)
        {
            _status.Text = "Extração cancelada.";
            Log($"CANCELADA: {ex}");
        }
        catch (Exception ex)
        {
            _status.Text = $"Falha na extração: {ex.Message}";
            Log($"ERRO FATAL: {ex}");
        }
        finally
        {
            if (tempPath is not null) try { File.Delete(tempPath); } catch { }
            Log("Processo de extração finalizado.");
            _choosePackage.IsEnabled = _chooseDestination.IsEnabled = true;
            _extract.IsEnabled = _package is not null && _destination is not null;
        }
    }

    private sealed class StorageFolderExtractionDestination : IExtractionDestination
    {
        private readonly IStorageFolder _root;
        private readonly Dictionary<Stream, IStorageFile> _files = new();

        public StorageFolderExtractionDestination(IStorageFolder root) => _root = root;

        public async Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(s => s is "." or ".."))
                throw new PkgExtractionException($"Caminho interno inseguro: {relativePath}");

            IStorageFolder folder = _root;
            foreach (var segment in segments[..^1])
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { folder = await folder.GetFolderAsync(segment); }
                catch { folder = await folder.CreateFolderAsync(segment); }
            }

            var temporaryName = $".{segments[^1]}.{Guid.NewGuid():N}.orbisextract.tmp";
            var file = await folder.CreateFileAsync(temporaryName);
            var stream = await file.OpenWriteAsync();
            _files[stream] = file;
            return stream;
        }

        public async Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_files.Remove(temporaryFile, out var file))
                throw new PkgExtractionException("Arquivo temporário de destino inválido.");
            await temporaryFile.FlushAsync(cancellationToken);
            await temporaryFile.DisposeAsync();

            var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            IStorageFolder folder = _root;
            foreach (var segment in segments[..^1])
                folder = await folder.GetFolderAsync(segment);

            var finalFile = await folder.CreateFileAsync(segments[^1]);
            await using var input = await file.OpenReadAsync();
            await using var output = await finalFile.OpenWriteAsync();
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
            await file.DeleteAsync();
        }

        public async Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default)
        {
            if (_files.Remove(temporaryFile, out var file))
            {
                await temporaryFile.DisposeAsync();
                await file.DeleteAsync();
            }
        }
    }
}
