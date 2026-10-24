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
    private const double WideBreakpoint = 720;   // largura da janela: sidebar x navegação inferior
    private const double TwoColumnBreakpoint = 640; // largura do conteúdo: 2 colunas x 1 coluna

    // Pacote
    private readonly TextBlock _fileName;
    private readonly WrapPanel _chips;
    private readonly StackPanel _infoRows;
    private readonly TextBlock _gameTitle, _version, _contentId;
    private readonly Button _choosePackage;

    // Configuração
    private readonly TextBlock _destinationName;
    private readonly Button _chooseDestination;
    private readonly TextBox _passcode;
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
    private readonly DispatcherTimer _timer;

    // Layout
    private readonly Grid _shell, _pageGrid;
    private readonly Control _sidebar, _bottomNav, _packageCard, _settingsCard, _monitorCard;
    private bool? _isWideShell, _isTwoColumn;

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

        // ---------- Cartão de configuração ----------
        _destinationName = UiTheme.Text("Nenhuma pasta escolhida", 14, UiTheme.TextSecondary);
        _chooseDestination = UiTheme.OutlinedButton("Escolher pasta");
        _chooseDestination.Click += async (_, _) => await ChooseDestinationAsync();
        _passcode = new TextBox
        {
            PlaceholderText = "Passcode (opcional)",
            PasswordChar = '•',
            Padding = new Thickness(14, 12),
            CornerRadius = new CornerRadius(12),
            Background = UiTheme.Brush(UiTheme.SurfaceElevated),
            Foreground = UiTheme.Brush(UiTheme.TextPrimary),
            BorderBrush = UiTheme.Brush(UiTheme.Outline)
        };
        _extract = UiTheme.PrimaryButton("Extrair arquivos");
        _extract.IsEnabled = false;
        _extract.Margin = new Thickness(0, 6, 0, 0);
        _extract.Click += async (_, _) => await ExtractAsync();

        _settingsCard = Section("Destino da extração",
            "Defina onde salvar os arquivos e informe o passcode, se necessário.",
            Field("Pasta de destino", _destinationName), _chooseDestination,
            Field("Passcode", _passcode), _extract);

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

        // ---------- Grid responsivo da página ----------
        _pageGrid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _pageGrid.Children.Add(_packageCard);
        _pageGrid.Children.Add(_settingsCard);
        _pageGrid.Children.Add(_monitorCard);

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
                        UiTheme.Text("Extrair pacote", isAndroid ? 28 : 32, UiTheme.TextPrimary, FontWeight.SemiBold),
                        UiTheme.Text("Gerencie o arquivo, o destino e o andamento em um só lugar.", 14, UiTheme.TextSecondary)
                    }
                },
                _pageGrid,
                UiTheme.Text("Use apenas pacotes e chaves que você tem autorização para acessar.", 12, UiTheme.TextDisabled)
            }
        };
        var centered = new Border { MaxWidth = 1000, HorizontalAlignment = HorizontalAlignment.Stretch, Child = page };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = centered
        };
        scroll.SizeChanged += (_, e) => ApplyPageLayout(e.NewSize.Width);

        // ---------- Shell: sidebar (desktop) / barra inferior (estreito) ----------
        _sidebar = BuildSidebar();
        _bottomNav = BuildBottomNav();
        _shell = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("224,*"),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Background = UiTheme.Brush(UiTheme.Background)
        };
        Grid.SetRowSpan(_sidebar, 2);
        Grid.SetColumn(scroll, 1);
        Grid.SetRow(_bottomNav, 1);
        _shell.Children.Add(_sidebar);
        _shell.Children.Add(scroll);
        _shell.Children.Add(_bottomNav);
        SizeChanged += (_, e) => ApplyShellLayout(e.NewSize.Width);
        Content = _shell;

        ApplyShellLayout(isAndroid ? 400 : 1120);
        ApplyPageLayout(isAndroid ? 400 : 900);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _elapsed.Text = _clock.Elapsed.ToString(@"mm\:ss");
        SetProgressIdle();
        Log("[INFO]  OrbisExtract iniciado.");
    }

    // ===================== Layout =====================

    private void ApplyShellLayout(double width)
    {
        var wide = width >= WideBreakpoint;
        if (_isWideShell == wide) return;
        _isWideShell = wide;
        _sidebar.IsVisible = wide;
        _bottomNav.IsVisible = !wide;
        var scroll = (Control)_shell.Children[1];
        Grid.SetColumn(scroll, wide ? 1 : 0);
        Grid.SetColumnSpan(scroll, wide ? 1 : 2);
        Grid.SetColumnSpan(_bottomNav, 2);
    }

    private void ApplyPageLayout(double width)
    {
        var twoColumns = width >= TwoColumnBreakpoint;
        if (_isTwoColumn == twoColumns) return;
        _isTwoColumn = twoColumns;

        _pageGrid.ColumnDefinitions = new ColumnDefinitions(twoColumns ? "*,*" : "*");
        _pageGrid.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
        Place(_packageCard, 0, 0, 1);
        Place(_settingsCard, twoColumns ? 0 : 1, twoColumns ? 1 : 0, 1);
        Place(_monitorCard, twoColumns ? 1 : 2, 0, twoColumns ? 2 : 1);
    }

    private static void Place(Control c, int row, int col, int colSpan)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        Grid.SetColumnSpan(c, colSpan);
    }

    private static Control BuildSidebar()
    {
        var top = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                UiTheme.Text("ORBIS", 12, UiTheme.Primary, FontWeight.Bold),
                UiTheme.Text("OrbisExtract", 20, UiTheme.TextPrimary, FontWeight.SemiBold),
                new Border { Height = 18 },
                NavItem("◈", "Extrair", selected: true),
                NavItem("◷", "Histórico", enabled: false),
                NavItem("⚙", "Configurações", enabled: false)
            }
        };
        ((TextBlock)top.Children[0]).Margin = new Thickness(12, 0, 0, 0);
        ((TextBlock)top.Children[1]).Margin = new Thickness(12, 0, 0, 0);

        var bottom = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Bottom,
            Children =
            {
                new Border { Height = 1, Background = UiTheme.Brush(UiTheme.Outline), Margin = new Thickness(0, 0, 0, 12) },
                UiTheme.Text("●  Aplicativo pronto", 12, UiTheme.Success),
                UiTheme.Text("PKG · PlayStation 4", 11, UiTheme.TextSecondary)
            }
        };
        foreach (var c in bottom.Children.OfType<TextBlock>()) c.Margin = new Thickness(12, 0);

        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);
        dock.Children.Add(top);

        return new Border
        {
            Background = UiTheme.Brush(UiTheme.Surface),
            BorderBrush = UiTheme.Brush(UiTheme.Outline),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(16, 26),
            Child = dock
        };
    }

    private static Button NavItem(string glyph, string label, bool selected = false, bool enabled = true) => new()
    {
        IsEnabled = enabled,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Left,
        Background = selected ? UiTheme.Brush(UiTheme.PrimaryContainer) : Brushes.Transparent,
        Foreground = UiTheme.Brush(selected ? UiTheme.Primary : enabled ? UiTheme.TextSecondary : UiTheme.TextDisabled),
        BorderThickness = new Thickness(0),
        CornerRadius = new CornerRadius(14),
        Padding = new Thickness(14, 13),
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = glyph, FontSize = 16, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center }
            }
        }
    };

    private static Control BuildBottomNav()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") };
        (string glyph, string label, bool selected)[] items =
            [("◈", "Extrair", true), ("◷", "Histórico", false), ("⚙", "Configurações", false)];
        for (var i = 0; i < items.Length; i++)
        {
            var (glyph, label, selected) = items[i];
            var color = selected ? UiTheme.Primary : UiTheme.TextDisabled;
            var item = new Button
            {
                IsEnabled = selected,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 8),
                Content = new StackPanel
                {
                    Spacing = 4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new Border
                        {
                            CornerRadius = new CornerRadius(16),
                            Padding = new Thickness(20, 4),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Background = selected ? UiTheme.Brush(UiTheme.PrimaryContainer) : Brushes.Transparent,
                            Child = new TextBlock { Text = glyph, FontSize = 18, Foreground = UiTheme.Brush(color), HorizontalAlignment = HorizontalAlignment.Center }
                        },
                        new TextBlock { Text = label, FontSize = 12, Foreground = UiTheme.Brush(color), HorizontalAlignment = HorizontalAlignment.Center }
                    }
                }
            };
            Grid.SetColumn(item, i);
            grid.Children.Add(item);
        }
        return new Border
        {
            Background = UiTheme.Brush(UiTheme.Surface),
            BorderBrush = UiTheme.Brush(UiTheme.Outline),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 4),
            Child = grid
        };
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

        _choosePackage.IsEnabled = _chooseDestination.IsEnabled = _extract.IsEnabled = _passcode.IsEnabled = false;
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
        _timer.Start();

        Log("--------------------------------------------------");
        Log($"Iniciando extração. Pacote: {_package.Name}");
        Log($"Destino: {_destination.Name}");
        Log($"Passcode informado: {!string.IsNullOrWhiteSpace(_passcode.Text)}");

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
                    string.IsNullOrWhiteSpace(_passcode.Text) ? null : _passcode.Text,
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
            _timer.Stop();
            _elapsed.Text = _clock.Elapsed.ToString(@"mm\:ss");
            _cancel.IsVisible = false;
            _run?.Dispose();
            _run = null;
            if (tempPath is not null) try { File.Delete(tempPath); } catch { }
            Log("Processo de extração finalizado.");
            _choosePackage.IsEnabled = _chooseDestination.IsEnabled = _passcode.IsEnabled = true;
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
