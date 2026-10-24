using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using OrbisExtract.Core;
using OrbisExtract.Infrastructure.OpenOrbis;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Queue<string> _logLines = new();
    private const int MaxLogLines = 250;

    public MainView()
    {
        Background = new SolidColorBrush(Color.Parse("#0B0F14"));
        Foreground = Brushes.White;

        _fileName = Label("Nenhum pacote selecionado", 14, "#AAB4C3");
        _metadata = Label("Selecione um PKG para consultar os metadados.", 13, "#AAB4C3");
        _status = Label("Pronto para começar.", 13, "#F5F7FA");
        _progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 8,
            IsVisible = false,
            Foreground = new SolidColorBrush(Color.Parse("#FF8A65")),
            Background = new SolidColorBrush(Color.Parse("#2B3542"))
        };
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
            Foreground = new SolidColorBrush(Color.Parse("#F5F7FA")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2B3542")),
            Text = $"{DateTime.Now:HH:mm:ss}  [INFO]  OrbisExtract iniciado."
        };
        _logLines.Enqueue(_log.Text);
        _passcode = new TextBox
        {
            PlaceholderText = "Passcode (opcional)",
            PasswordChar = '•',
            Padding = new Thickness(12),
            Background = new SolidColorBrush(Color.Parse("#1D2530")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#2B3542"))
        };

        _choosePackage = ActionButton("Abrir arquivo PKG", "#B9A7FF");
        _choosePackage.Click += async (_, _) => await ChoosePackageAsync();
        _chooseDestination = ActionButton("Pasta de destino", "#222834");
        _chooseDestination.Click += async (_, _) => await ChooseDestinationAsync();
        _extract = ActionButton("Extrair arquivos", "#B9A7FF");
        _extract.IsEnabled = false;
        _extract.Click += async (_, _) => await ExtractAsync();

        var packageCard = SectionCard("ARQUIVO DO PACOTE",
            "Selecione o PKG para consultar os metadados antes de extrair.",
            _fileName, _metadata, _choosePackage);

        var settingsCard = SectionCard("DESTINO E CHAVE",
            "Defina onde salvar os arquivos e informe o passcode, se necessário.",
            Label("PASTA DE DESTINO", 11, "#AAB4C3"),
            _chooseDestination,
            Label("PASSCODE (OPCIONAL)", 11, "#AAB4C3"),
            _passcode);

        var operationCard = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#151B24")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2B3542")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    Label("PROGRESSO DA EXTRAÇÃO", 11, "#AAB4C3"),
                    new TextBlock
                    {
                        Text = "Acompanhe aqui o andamento do processo.",
                        FontSize = 15,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = new SolidColorBrush(Color.Parse("#F5F7FA")),
                        TextWrapping = TextWrapping.Wrap
                    },
                    _extract,
                    _progress,
                    _status,
                    new Expander
                    {
                        Header = "Ver log detalhado",
                        IsExpanded = false,
                        Foreground = new SolidColorBrush(Color.Parse("#AAB4C3")),
                        Content = _log
                    }
                }
            }
        };

        var desktopColumns = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 16,
            RowSpacing = 16
        };
        Grid.SetColumn(packageCard, 0);
        Grid.SetRow(packageCard, 0);
        Grid.SetColumn(settingsCard, 1);
        Grid.SetRow(settingsCard, 0);
        Grid.SetColumn(operationCard, 0);
        Grid.SetRow(operationCard, 1);
        Grid.SetColumnSpan(operationCard, 2);
        desktopColumns.Children.Add(packageCard);
        desktopColumns.Children.Add(settingsCard);
        desktopColumns.Children.Add(operationCard);

        var mobileLayout = new StackPanel
        {
            Spacing = 14,
            Children = { packageCard, settingsCard, operationCard }
        };

        var isAndroid = OperatingSystem.IsAndroid();
        var mainContent = new StackPanel
        {
            Spacing = 22,
            Margin = isAndroid ? new Thickness(16, 22, 16, 28) : new Thickness(28, 30),
            MaxWidth = 980,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                new StackPanel
                {
                    Spacing = 7,
                    Children =
                    {
                        Label("WORKSPACE  /  EXTRAÇÃO", 11, "#B9A7FF"),
                        new TextBlock
                        {
                            Text = "Extrair pacote",
                            FontSize = isAndroid ? 27 : 32,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#F5F7FA"))
                        },
                        new TextBlock
                        {
                            Text = "Gerencie o arquivo, o destino e o andamento em um só lugar.",
                            FontSize = 14,
                            Foreground = new SolidColorBrush(Color.Parse("#AAB4C3")),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                },
                isAndroid ? mobileLayout : desktopColumns,
                new TextBlock
                {
                    Text = "Use apenas pacotes e chaves que você tem autorização para acessar.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#667181")),
                    FontSize = 12
                }
            }
        };

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = mainContent
        };
    }

    private static Border SectionCard(string title, string description, params Control[] controls)
    {
        var panel = new StackPanel { Spacing = 13 };
        panel.Children.Add(Label(title, 11, "#B9A7FF"));
        panel.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#AAB4C3")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 4)
        });
        foreach (var control in controls)
            panel.Children.Add(control);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#151B24")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2B3542")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(18),
            Child = panel
        };
    }

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        Dispatcher.UIThread.Post(() =>
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines)
                _logLines.Dequeue();
            _log.Text = string.Join(Environment.NewLine, _logLines);
            _log.CaretIndex = _log.Text.Length;
        });
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
                Log("Metadados do PKG lidos.");
                _metadata.Text = $"{info.Title}\nVersão: {info.Version}\nContent ID: {info.ContentId}";
            }
        }
        catch (Exception ex)
        {
            _metadata.Text = "Metadados indisponíveis; o pacote ainda pode ser tentado.";
            _status.Text = ex.Message;
            Log($"Aviso ao ler metadados: {ex.Message}");
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
            Log("Cópia temporária concluída.");

            await using var packageStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true);
            Log("Abrindo e validando a estrutura interna do PKG…");
            var destination = new StorageFolderExtractionDestination(_destination, Log);
            var progress = new Progress<ExtractionProgress>(p =>
            {
                var total = p.FilesTotal ?? 0;
                _progress.Value = total > 0
                    ? Math.Clamp(p.FilesCompleted * 100d / total, 0, 100)
                    : 0;
                _status.Text = p.State == "Concluído"
                    ? $"Concluído · {p.FilesCompleted:N0} arquivos"
                    : $"{p.State} · {p.FilesCompleted:N0}/{total:N0} arquivos";
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
            Log("Extração cancelada.");
        }
        catch (Exception ex)
        {
            _status.Text = $"Falha na extração: {ex.Message}";
            Log($"Falha na extração: {ex.Message}");
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
        private readonly Action<string> _log;
        private readonly Dictionary<Stream, IStorageFile> _files = new();

        public StorageFolderExtractionDestination(IStorageFolder root, Action<string> log)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _log = log ?? (_ => { });
        }

        public async Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(s => s is "." or ".."))
                throw new PkgExtractionException($"Caminho interno inseguro: {relativePath}");

            IStorageFolder? folder = _root;
            if (folder is null)
                throw new PkgExtractionException("O provedor de armazenamento não forneceu a pasta de destino.");

            foreach (var segment in segments[..^1])
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parent = folder ?? throw new PkgExtractionException($"Pasta pai inválida ao criar '{segment}'.");
                IStorageFolder? child = null;
                try { child = await parent.GetFolderAsync(segment); }
                catch (Exception ex)
                {
                    _log($"Pasta '{segment}' ainda não existe ou não pôde ser aberta: {ex.Message}");
                }

                if (child is null)
                {
                    _log($"Criando diretório de saída: {segment}");
                    child = await parent.CreateFolderAsync(segment);
                }

                folder = child ?? throw new PkgExtractionException($"O provedor de armazenamento não conseguiu abrir nem criar o diretório '{segment}'.");
            }

            var targetFolder = folder ?? throw new PkgExtractionException($"Pasta de destino inválida para '{relativePath}'.");
            var temporaryName = $".{segments[^1]}.{Guid.NewGuid():N}.orbisextract.tmp";
            _log($"Criando arquivo temporário de saída: {relativePath}");
            var file = await targetFolder.CreateFileAsync(temporaryName);
            if (file is null)
                throw new PkgExtractionException($"O provedor de armazenamento não criou o arquivo temporário para '{relativePath}'.");
            var stream = await file.OpenWriteAsync();
            if (stream is null)
                throw new PkgExtractionException($"Não foi possível abrir o arquivo temporário para escrita: '{relativePath}'.");
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
