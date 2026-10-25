using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OrbisExtract.Core;
using OrbisExtract.Infrastructure.OpenOrbis;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Button = Avalonia.Controls.Button;
using ProgressBar = Avalonia.Controls.ProgressBar;

namespace OrbisExtract.App.Views;

public sealed class MainView : UserControl
{
    private const int MaxLogLines = 250;

    // Pacote
    private readonly TextBlock _fileName;
    private readonly WrapPanel _chips;
    private readonly StackPanel _infoRows;
    private readonly TextBlock _gameTitle, _version, _contentId;
    private readonly Button _choosePackage;

    // Configuração
    private readonly TextBlock _destinationName;
    private readonly Button _chooseDestination;
    private readonly Button _extract;

    // Monitor
    private readonly TextBlock _stateTitle, _counter, _percent, _elapsed, _status;
    private readonly ProgressBar _progress;
    private readonly Button _cancel;
    private readonly StackPanel _logPanel;
    private readonly ScrollViewer _logScroll;
    private readonly Border _banner;
    private readonly TextBlock _bannerText;
    private readonly Stopwatch _clock = new();

    // Layout
    private readonly Grid _mainGrid, _contentGrid;
    private readonly Control _packageCard, _settingsCard, _monitorCard;
    private bool? _isWideContent;

    // Estado
    private IStorageFile? _package;
    private IStorageFolder? _destination;
    private CancellationTokenSource? _run;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Queue<TextBlock> _logLines = new();

    public MainView()
    {
        Background = UiTheme.Brush(UiTheme.Background);
        Foreground = UiTheme.Brush(UiTheme.TextPrimary);

        // ---------- Cartão do pacote ----------
        _fileName = UiTheme.Text("Nenhum pacote selecionado", 15, UiTheme.TextSecondary, FontWeight.Medium);
        _fileName.TextWrapping = TextWrapping.WrapWithOverflow;
        _chips = new WrapPanel { IsVisible = false, Margin = new Thickness(0, 12, 0, 0) };
        _gameTitle = UiTheme.Text("", 15, UiTheme.TextPrimary, FontWeight.SemiBold);
        _version = UiTheme.Text("", 14);
        _contentId = UiTheme.Text("", 13);
        _contentId.FontFamily = UiTheme.Mono;
        _infoRows = new StackPanel
        {
            Spacing = 10,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { Field("Jogo", _gameTitle), Field("Versão", _version), Field("Content ID", _contentId) }
        };
        _choosePackage = UiTheme.OutlinedButton("Selecionar arquivo PKG");
        _choosePackage.Click += async (_, _) => await ChoosePackageAsync();

        _packageCard = Section("Arquivo do pacote",
            "Selecione o PKG para consultar os metadados antes de extrair.",
            _fileName, _chips, _infoRows, _choosePackage);

        // ---------- Cartão de configuração (passcode removido da UI) ----------
        _destinationName = UiTheme.Text("Nenhuma pasta escolhida", 14, UiTheme.TextSecondary);
        _chooseDestination = UiTheme.OutlinedButton("Escolher pasta");
        _chooseDestination.Click += async (_, _) => await ChooseDestinationAsync();
        _extract = UiTheme.PrimaryButton("Extrair arquivos");
        _extract.IsEnabled = false;
        _extract.Margin = new Thickness(0, 6, 0, 0);
        _extract.Click += async (_, _) => await ExtractAsync();

        _settingsCard = Section("Configuração",
            "Defina onde salvar os arquivos extraídos.",
            Field("Pasta de destino", _destinationName), _chooseDestination,
            _extract);

        // ---------- Monitor de extração ----------
        _stateTitle = UiTheme.Text("Pronto para extrair", 18, UiTheme.TextPrimary, FontWeight.SemiBold);
        _percent = UiTheme.Text("", 18, UiTheme.Progress, FontWeight.SemiBold);
        _counter = UiTheme.Text("Aguardando seleção de arquivo e destino.", 13, UiTheme.TextSecondary);
        _status = UiTheme.Text("", 13, UiTheme.TextSecondary);
        _status.IsVisible = false;
        _elapsed = UiTheme.Text("00:00", 13, UiTheme.TextSecondary);
        _elapsed.IsVisible = false;
        _progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Foreground = UiTheme.Brush(UiTheme.Progress),
            Background = UiTheme.Brush(UiTheme.Outline)
        };
        _cancel = UiTheme.OutlinedButton("Cancelar");
        _cancel.IsVisible = false;
        _cancel.HorizontalAlignment = HorizontalAlignment.Left;
        _cancel.Foreground = UiTheme.Brush(UiTheme.Error);
        _cancel.Click += (_, _) => _run?.Cancel();

        _logPanel = new StackPanel { Spacing = 2 };
        _logScroll = new ScrollViewer
        {
            MaxHeight = 260,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _logPanel
        };
        var logBox = new Border
        {
            Background = UiTheme.Brush("#0D1017"),
            BorderBrush = UiTheme.Brush(UiTheme.Outline),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12),
            MinHeight = 120,
            Child = _logScroll
        };
        var expander = new Expander
        {
            Header = "Ver log detalhado",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Foreground = UiTheme.Brush(UiTheme.TextSecondary),
            Content = logBox
        };

        _bannerText = UiTheme.Text("", 13, UiTheme.TextPrimary);
        _banner = new Border
        {
            IsVisible = false,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10),
            BorderThickness = new Thickness(1),
            Child = _bannerText
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(_stateTitle);
        Grid.SetColumn(_percent, 1);
        header.Children.Add(_percent);

        var metaRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        metaRow.Children.Add(_counter);
        Grid.SetColumn(_elapsed, 1);
        metaRow.Children.Add(_elapsed);

        _monitorCard = UiTheme.Card(new StackPanel
        {
            Spacing = 14,
            Children = { header, _progress, metaRow, _status, _banner, _cancel, expander }
        });

        // ---------- Grid responsivo do conteúdo ----------
        _contentGrid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _contentGrid.Children.Add(_packageCard);
        _contentGrid.Children.Add(_settingsCard);
        _contentGrid.Children.Add(_monitorCard);

        var mainGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*") };
        var isAndroid = OperatingSystem.IsAndroid();
        var page = new StackPanel
        {
            Spacing = 24,
            Margin = isAndroid ? new Thickness(16, 20, 16, 24) : new Thickness(32, 32, 32, 28),
            Children =
            {
                new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        UiTheme.Text("Extrair PKG", isAndroid ? 28 : 32, UiTheme.TextPrimary, FontWeight.SemiBold),
                        UiTheme.Text("Ferramenta de extração para pacotes PlayStation 4.", 14, UiTheme.TextSecondary)
                    }
                },
                _contentGrid,
                UiTheme.Text("Use apenas pacotes e chaves que você tem autorização para acessar.", 12, UiTheme.TextDisabled)
            }
        };
        var centered = new Border { MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Stretch, Child = page };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = centered
        };
        scroll.SizeChanged += (_, e) => ApplyContentLayout(e.NewSize.Width);

        // ---------- Layout principal (coluna única simplificada) ----------
        _mainGrid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(scroll, 0);
        Grid.SetRow(_mainGrid.Children[1], 1);
        mainGrid.Children.Add(scroll);
        Content = mainGrid;

        ApplyContentLayout(isAndroid ? 400 : 900);
        SetProgressIdle();
        Log("[INFO]  OrbisExtract iniciado.");
    }

    // ===================== Layout =====================

    private void ApplyContentLayout(double width)
    {
        var isWide = width >= 600;
        if (_isWideContent == isWide) return;
        _isWideContent = isWide;

        _contentGrid.ColumnDefinitions = new ColumnDefinitions(isWide ? "*,*" : "*");
        _contentGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
        Place(_packageCard, 0, 0, 1);
        Place(_settingsCard, isWide ? 0 : 1, isWide ? 1 : 0, 1);
        Place(_monitorCard, isWide ? 1 : 2, 0, isWide ? 2 : 1);
    }

    private static void Place(Control c, int row, int col, int colSpan)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        Grid.SetColumnSpan(c, colSpan);
    }

    // ===================== Helpers de UI =====================

    private static Border Section(string title, string description, params Control[] controls)
    {
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(UiTheme.Text(title, 18, UiTheme.TextPrimary, FontWeight.SemiBold));
        panel.Children.Add(UiTheme.Text(description, 13, UiTheme.TextSecondary));
        foreach (var c in controls) panel.Children.Add(c);
        return UiTheme.Card(panel);
    }

    private static StackPanel Field(string label, Control value) => new()
    {
        Spacing = 4,
        Children = { UiTheme.Text(label, 12, UiTheme.TextSecondary, FontWeight.Medium), value }
    };

    private void ShowBanner(string? message, string color = UiTheme.Error)
    {
        _banner.IsVisible = !string.IsNullOrEmpty(message);
        if (message is null) return;
        _bannerText.Text = message;
        _banner.BorderBrush = UiTheme.Brush(color);
        _banner.Background = new SolidColorBrush(Color.Parse(color), 0.12);
    }

    private void SetProgressIdle()
    {
        _progress.IsIndeterminate = false;
        _progress.Value = 0;
        _percent.Text = "";
    }

    private void Log(string message)
    {
        var time = $"{DateTime.Now:HH:mm:ss}";
        Dispatcher.UIThread.Post(() =>
        {
            var color = message.Contains("Falha", StringComparison.OrdinalIgnoreCase) || message.Contains("[ERRO", StringComparison.OrdinalIgnoreCase)
                ? UiTheme.Error
                : message.Contains("Aviso", StringComparison.OrdinalIgnoreCase) || message.Contains("cancelad", StringComparison.OrdinalIgnoreCase)
                    ? UiTheme.Warning
                    : message.Contains("concluíd", StringComparison.OrdinalIgnoreCase)
                        ? UiTheme.Success
                        : UiTheme.TextSecondary;
            var line = UiTheme.Text($"{time}  {message}", 12, color);
            line.FontFamily = UiTheme.Mono;
            _logPanel.Children.Add(line);
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines)
                _logPanel.Children.Remove(_logLines.Dequeue());
            _logScroll.ScrollToEnd();
        });
    }

    private static string FormatSize(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var i = 0;
        while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
        return $"{value:0.#} {units[i]}";
    }

    // ===================== Ações =====================

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
        _fileName.Foreground = UiTheme.Brush(UiTheme.TextPrimary);
        _infoRows.IsVisible = false;
        _chips.Children.Clear();
        _chips.Children.Add(UiTheme.Chip("PS4"));
        _chips.IsVisible = true;
        _counter.Text = "Arquivo selecionado.";
        ShowBanner(null);
        Log($"Pacote selecionado: {_package.Name}");
        _extract.IsEnabled = false;

        try
        {
            var props = await _package.GetBasicPropertiesAsync();
            if (props.Size is { } size) _chips.Children.Add(UiTheme.Chip(FormatSize(size)));
        }
        catch { /* tamanho é opcional */ }

        try
        {
            await using var stream = await _package.OpenReadAsync();
            if (!stream.CanSeek)
            {
                _counter.Text = "Metadados serão validados durante a extração.";
            }
            else
            {
                Log("Lendo metadados do PKG…");
                var info = await new OpenOrbisPkgMetadataReader().ReadAsync(stream, _lifetime.Token);
                Log("Metadados do PKG lidos.");
                _gameTitle.Text = info.Title?.ToString() ?? "—";
                _version.Text = info.Version?.ToString() ?? "—";
                _contentId.Text = info.ContentId?.ToString() ?? "—";
                _infoRows.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            ShowBanner("Metadados indisponíveis; o pacote ainda pode ser tentado.", UiTheme.Warning);
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
        _destinationName.Text = _destination.Name;
        _destinationName.Foreground = UiTheme.Brush(UiTheme.TextPrimary);
        _extract.IsEnabled = _package is not null;
        Log($"Destino selecionado: {_destination.Name}");
    }

    private async Task ExtractAsync()
    {
        if (_package is null || _destination is null) return;

        _choosePackage.IsEnabled = _chooseDestination.IsEnabled = _extract.IsEnabled = false;
        _run = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var token = _run.Token;

        ShowBanner(null);
        _cancel.IsVisible = _elapsed.IsVisible = _status.IsVisible = true;
        _stateTitle.Text = "Preparando";
        _counter.Text = "Preparando a extração…";
        _status.Text = "";
        _progress.IsIndeterminate = true;
        _percent.Text = "";
        _elapsed.Text = "00:00";
        _clock.Restart();

        Log("--------------------------------------------------");
        Log($"Iniciando extração. Pacote: {_package.Name}");
        Log($"Destino: {_destination.Name}");
        Log("Passcode: não utilizado nesta versão.");

        string? tempPath = null;
        try
        {
            // Streams content:// do Android nem sempre permitem seek. Copiamos o PKG
            // para um arquivo temporário privado para que o LibOrbisPkg possa lê-lo.
            tempPath = Path.Combine(Path.GetTempPath(), $"orbisextract-{Guid.NewGuid():N}.pkg");
            _stateTitle.Text = "Copiando pacote";
            Log("Copiando o PKG para um arquivo temporário pesquisável…");
            await using (var source = await _package.OpenReadAsync())
            await using (var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024, true))
                await source.CopyToAsync(temp, token);
            Log("Cópia temporária concluída.");

            await using var packageStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true);
            Log("Abrindo e validando a estrutura interna do PKG…");
            var destination = new StorageFolderExtractionDestination(_destination, Log);
            var progress = new Progress<ExtractionProgress>(p =>
            {
                var total = p.FilesTotal ?? 0;
                var pct = total > 0 ? Math.Clamp(p.FilesCompleted * 100d / total, 0, 100) : 0;
                _progress.IsIndeterminate = total <= 0;
                _progress.Value = pct;
                _percent.Text = total > 0 ? $"{pct:0}%" : "";
                _stateTitle.Text = p.State == "Concluído" ? "Concluindo" : p.State;
                _counter.Text = total > 0
                    ? $"{p.FilesCompleted:N0} de {total:N0} arquivos"
                    : $"{p.FilesCompleted:N0} arquivos";
            });
            var result = await new OpenOrbisPkgExtractor().ExtractAsync(
                new ExtractionRequest(packageStream, string.Empty,
                    null, // Passcode removido da interface
                    Destination: destination),
                progress,
                token);

            _progress.IsIndeterminate = false;
            _progress.Value = 100;
            _percent.Text = "100%";
            _stateTitle.Text = "Concluído";
            _counter.Text = $"{result.FilesExtracted:N0} arquivos · {result.BytesExtracted / 1024d / 1024d:N1} MiB";
            ShowBanner($"Extração concluída com sucesso em {_clock.Elapsed:mm\\:ss}.", UiTheme.Success);
            Log($"Extração concluída. Arquivos: {result.FilesExtracted:N0}; bytes: {result.BytesExtracted:N0}.");
        }
        catch (OperationCanceledException)
        {
            _progress.IsIndeterminate = false;
            _stateTitle.Text = "Cancelado";
            _counter.Text = "A extração foi interrompida.";
            ShowBanner("Extração cancelada.", UiTheme.Warning);
            Log("Extração cancelada.");
        }
        catch (Exception ex)
        {
            _progress.IsIndeterminate = false;
            _stateTitle.Text = "Falha na extração";
            _counter.Text = "Veja o log detalhado para mais informações.";
            ShowBanner($"Falha na extração: {ex.Message}");
            Log($"Falha na extração: {ex.Message}");
        }
        finally
        {
            _clock.Stop();
            _elapsed.Text = _clock.Elapsed.ToString(@"mm\:ss");
            _cancel.IsVisible = false;
            _run?.Dispose();
            _run = null;
            if (tempPath is not null) try { File.Delete(tempPath); } catch { }
            Log("Processo de extração finalizado.");
            _choosePackage.IsEnabled = _chooseDestination.IsEnabled = true;
            _extract.IsEnabled = _package is not null && _destination is not null;
        }
    }

    // ===================== Destino de extração (inalterado) =====================

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
