using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private readonly Grid _contentGrid;
    private readonly Control _packageCard, _settingsCard, _monitorCard;
    private bool? _isWideContent;
    private readonly Border _aboutOverlay;

    // Estado
    private IStorageFile? _package;
    private IStorageFolder? _destination;
    private CancellationTokenSource? _run;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Queue<Control> _logLines = new();
    private readonly List<string> _logMessages = new();

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
        _cancel.Foreground = UiTheme.Brush(UiTheme.Primary);
        _cancel.Click += (_, _) => _run?.Cancel();

        _logPanel = new StackPanel { Spacing = 0 };
        _logScroll = new ScrollViewer
        {
            MaxHeight = 260,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _logPanel
        };
        var logBox = new Border
        {
            Background = UiTheme.Brush(UiTheme.Surface),
            BorderBrush = UiTheme.Brush(UiTheme.Outline),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14),
            MinHeight = 112,
            Child = new StackPanel
            {
                Spacing = 8,
                Children = { _logScroll }
            }
        };
        _logScroll.MaxHeight = 260;
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
            Spacing = 10,
            Children = { header, _progress, metaRow, _status, _banner, _cancel, logBox }
        });

        // ---------- Grid responsivo do conteúdo ----------
        _contentGrid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _contentGrid.Children.Add(_packageCard);
        _contentGrid.Children.Add(_settingsCard);
        _contentGrid.Children.Add(_monitorCard);

        var mainGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*") };
        _aboutOverlay = CreateAboutOverlay();
        mainGrid.Children.Add(_aboutOverlay);
        Panel.SetZIndex(_aboutOverlay, 10);
        var isAndroid = OperatingSystem.IsAndroid();
        var page = new StackPanel
        {
            Spacing = 18,
            Margin = isAndroid ? new Thickness(14, 14, 14, 18) : new Thickness(28, 28, 28, 24),
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 12,
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
                        CreateAboutButton()
                    }
                },
                _contentGrid,
            }
        };
        var centered = new Border { MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Stretch, Child = page };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = centered
        };
        scroll.SizeChanged += (_, e) => ApplyContentLayout(e.NewSize.Width);

        // ---------- Layout principal (coluna única simplificada) ----------
        var wrapper = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        mainGrid.Children.Add(scroll);
        Grid.SetRow(scroll, 0);
        wrapper.Children.Add(mainGrid);
        Content = wrapper;

        ApplyContentLayout(isAndroid ? 400 : 900);
        SetProgressIdle();
        Log("[INFO]  OrbisExtract iniciado.");
    }

    private Button CreateAboutButton()
    {
        var button = UiTheme.OutlinedButton("ⓘ");
        button.Width = 44;
        button.Height = 44;
        button.MinWidth = 44;
        button.MinHeight = 44;
        button.Padding = new Thickness(0);
        button.VerticalAlignment = VerticalAlignment.Center;
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.FontSize = 22;
        button.Click += (_, _) => _aboutOverlay.IsVisible = true;
        Grid.SetColumn(button, 1);
        return button;
    }

    private Border CreateAboutOverlay()
    {
        const string license = "MIT License\n\nCopyright (c) 2026 flugeru\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the Software), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\nThe above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\nTHE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.";
        var close = UiTheme.OutlinedButton("Fechar");
        var licenseBox = new TextBox
        {
            Text = license,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 150,
            MaxHeight = 200,
            Background = UiTheme.Brush(UiTheme.Background),
            Foreground = UiTheme.Brush(UiTheme.TextSecondary),
            BorderBrush = UiTheme.Brush(UiTheme.Outline)
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                UiTheme.Text("Sobre o OrbisExtract", 22, UiTheme.TextPrimary, FontWeight.SemiBold),
                UiTheme.Text("Versão 1.0.1", 14, UiTheme.TextSecondary),
                UiTheme.Text("Repositório", 15, UiTheme.TextPrimary, FontWeight.SemiBold),
                UiTheme.Text("https://github.com/flugeru/OrbisExtract", 13, UiTheme.Primary),
                UiTheme.Text("Changelog", 15, UiTheme.TextPrimary, FontWeight.SemiBold),
                UiTheme.Text("• Melhorias na extração de PKG e no tratamento de arquivos.\n• Ajustes na interface e nos logs.\n• Correções de compatibilidade e estabilidade.", 13, UiTheme.TextSecondary),
                UiTheme.Text("Licença MIT", 15, UiTheme.TextPrimary, FontWeight.SemiBold),
                licenseBox,
                close
            }
        };
        close.Click += (_, _) => _aboutOverlay.IsVisible = false;
        var dialogCard = new Border
        {
            MaxWidth = 520,
            MaxHeight = 620,
            Margin = new Thickness(18),
            Child = UiTheme.Card(new ScrollViewer
            {
                Content = new Border { Padding = new Thickness(18), Child = content },
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            })
        };
        var overlayGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto,*"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,*")
        };
        overlayGrid.Children.Add(dialogCard);
        Grid.SetColumn(dialogCard, 1);
        Grid.SetRow(dialogCard, 1);
        return new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.Parse("#CC000000")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = overlayGrid
        };
    }

    // ===================== Layout =====================

    private void ApplyContentLayout(double width)
    {
        var isWide = !OperatingSystem.IsAndroid() && width >= 600;
        if (_isWideContent == isWide) return;
        _isWideContent = isWide;

        _contentGrid.ColumnDefinitions = new ColumnDefinitions(isWide ? "*,*" : "*");
        _contentGrid.RowDefinitions = new RowDefinitions(isWide ? "Auto,Auto" : "Auto,Auto,Auto");

        // Mobile: package, configuration, then extraction monitor in one column.
        // Desktop: package top-left, monitor top-right, configuration below the package.
        Place(_packageCard, 0, 0, 1);
        Place(_settingsCard, 1, 0, 1);
        Place(_monitorCard, isWide ? 0 : 2, isWide ? 1 : 0, 1);
        _packageCard.IsVisible = true;
        _settingsCard.IsVisible = true;
        _monitorCard.IsVisible = true;
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
            _logMessages.Add($"{time}  {message}");
            while (_logMessages.Count > MaxLogLines)
                _logMessages.RemoveAt(0);

            var isError = message.Contains("Falha", StringComparison.OrdinalIgnoreCase) || message.Contains("[ERRO", StringComparison.OrdinalIgnoreCase);
            var accent = isError ? UiTheme.Error : UiTheme.Primary;
            var marker = new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(4),
                Background = UiTheme.Brush(accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            var details = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    UiTheme.Text(time, 10, UiTheme.TextSecondary),
                    new TextBox
                    {
                        Text = message,
                        FontSize = 12,
                        Foreground = UiTheme.Brush(isError ? UiTheme.Error : UiTheme.Primary),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(0),
                        IsReadOnly = true,
                        AcceptsReturn = true,
                        TextWrapping = TextWrapping.Wrap,
                        MinHeight = 22,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    }
                }
            };
            var line = new Border
            {
                Padding = new Thickness(4, 8),
                BorderBrush = UiTheme.Brush(UiTheme.Outline),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = CreateLogRow(marker, details)
            };
            _logPanel.Children.Add(line);
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines)
                _logPanel.Children.Remove(_logLines.Dequeue());
            _logScroll.ScrollToEnd();
        });
    }

    private static Grid CreateLogRow(Control marker, Control details)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(marker);
        row.Children.Add(details);
        Grid.SetColumn(details, 1);
        return row;
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

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Selecionar pacote PlayStation PKG",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Pacotes PKG") { Patterns = ["*.pkg", "*.PKG"] },
                    FilePickerFileTypes.All
                ]
            });
        }
        catch (Exception ex)
        {
            // O seletor nativo pode falhar se o caminho retornado não existir mais no disco.
            ShowBanner($"Não foi possível abrir o seletor de arquivos: {ex.Message}", UiTheme.Warning);
            Log($"Aviso ao abrir o seletor de pacotes: {ex.Message}");
            return;
        }

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

        IReadOnlyList<IStorageFolder> folders;
        try
        {
            folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Escolher pasta para os arquivos extraídos",
                AllowMultiple = false
            });
        }
        catch (Exception ex)
        {
            // O Win32StorageProvider lança ArgumentException ("Directory must exist") quando o
            // caminho retornado pelo diálogo não existe mais no disco (ex.: pasta removida ou
            // item virtual). Sem este tratamento a exceção escapava para o handler async void
            // e encerrava o processo (aplicativo congelava e fechava).
            ShowBanner($"Não foi possível selecionar a pasta: {ex.Message}", UiTheme.Warning);
            Log($"Aviso ao abrir o seletor de pastas: {ex.Message}");
            return;
        }

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
        Stream? packageStream = null;
        System.Timers.Timer? timer = null;

        try
        {
            // Use o fluxo original diretamente quando ele permite seek. Alguns provedores
            // Android retornam content:// sem seek; só nesses casos o parser exige um arquivo temporário.
            var source = await _package.OpenReadAsync();
            if (source.CanSeek)
            {
                packageStream = source;
                Log("Lendo o PKG selecionado diretamente, sem copiá-lo.");
            }
            else
            {
                await source.DisposeAsync();
                tempPath = Path.Combine(Path.GetTempPath(), $"orbisextract-{Guid.NewGuid():N}.pkg");
                _stateTitle.Text = "Preparando pacote";
                Log("O provedor não permite acesso aleatório; preparando uma cópia temporária necessária para leitura.");
                await using var input = await _package.OpenReadAsync();
                await using (var temp = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, true))
                    await input.CopyToAsync(temp, token);
                packageStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
            }
            Log("Abrindo e validando a estrutura interna do PKG…");

            // Inicializa o timer para atualização contínua do temporizador
            timer = new System.Timers.Timer(200);
            timer.Elapsed += (s, e) => Dispatcher.UIThread.Post(() =>
            {
                if (_clock.IsRunning)
                    _elapsed.Text = _clock.Elapsed.ToString(@"mm\:ss");
            });
            timer.AutoReset = true;
            timer.Start();

            // Para Windows com destino local, criamos um LocalExtractionDestination
            // Para Android, usamos StorageFolderExtractionDestination
            // A criação roda em Task.Run para não bloquear a UI em caminhos de rede/lentos.
            IExtractionDestination destination = await Task.Run<IExtractionDestination>(() =>
            {
                if (!OperatingSystem.IsWindows())
                {
                    // Android - usa StorageFolderExtractionDestination
                    return new StorageFolderExtractionDestination(_destination, Log);
                }

                // Usa o caminho completo da pasta escolhida (não apenas o nome base)
                var destinationPath = _destination.TryGetLocalPath();
                if (destinationPath is null)
                    return new StorageFolderExtractionDestination(_destination, Log);

                // Verifica se o destino é acessível e cria um fallback se necessário
                var testPath = Path.Combine(destinationPath, "test_access");
                try
                {
                    Directory.CreateDirectory(testPath);
                    if (Directory.Exists(testPath)) Directory.Delete(testPath, true);
                    return new LocalExtractionDestination(destinationPath);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Falha de permissão - usa fallback com nome do pacote
                    Log($"Falha ao acessar destino: {ex.Message}. Usando fallback.");
                    var fallbackRoot = CreateFallbackRoot();
                    return new WindowsFallbackDestination(fallbackRoot, _package.Name, Log);
                }
            });

            // Notificação nativa de progresso (apenas Android; placeholder nas demais plataformas)
            OrbisExtract.App.Shared.AppNotificationHelper.StartExtraction(
                _package.Name, _destination.Name, 0, 0);

            var progress = new Progress<ExtractionProgress>(p =>
            {
                if (token.IsCancellationRequested)
                    return;

                OrbisExtract.App.Shared.AppNotificationHelper.UpdateProgress(
                    p.FilesCompleted, p.FilesTotal ?? 0, p.State);
                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

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
            });

            var result = await new OpenOrbisPkgExtractor().ExtractAsync(
                new ExtractionRequest(packageStream, string.Empty,
                    null, // Passcode removido da interface
                    Destination: destination),
                progress,
                token);

            timer?.Stop();
            timer?.Dispose();

            _progress.IsIndeterminate = false;
            _progress.Value = 100;
            _percent.Text = "100%";
            _stateTitle.Text = "Concluído";
            _counter.Text = $"{result.FilesExtracted:N0} arquivos · {result.BytesExtracted / 1024d / 1024d:N1} MiB";
            
            // Mostra notificação Android de sucesso se estiver no Android
            if (OperatingSystem.IsAndroid())
            {
                var top = TopLevel.GetTopLevel(this);
                if (top != null)
                {
                    OrbisExtract.App.Shared.AppNotificationHelper.CompleteExtraction(result.FilesExtracted, result.BytesExtracted);
                }
            }
            
            ShowBanner($"Extração concluída com sucesso em {_clock.Elapsed:mm\\:ss}.", UiTheme.Success);
            Log($"Extração concluída. Arquivos: {result.FilesExtracted:N0}; bytes: {result.BytesExtracted:N0}.");
        }
        catch (OperationCanceledException)
        {
            _progress.IsIndeterminate = false;
            _stateTitle.Text = "Cancelado";
            _counter.Text = "A extração foi interrompida.";
            ShowBanner("Extração cancelada.", UiTheme.Primary);
            Log("Extração cancelada.");
            
            // Mostra notificação Android de cancelamento se estiver no Android
            if (OperatingSystem.IsAndroid())
            {
                var top = TopLevel.GetTopLevel(this);
                if (top != null)
                {
                    OrbisExtract.App.Shared.AppNotificationHelper.CancelExtraction();
                }
            }
        }
        catch (Exception ex)
        {
            _progress.IsIndeterminate = false;
            _stateTitle.Text = "Falha na extração";
            _counter.Text = "Veja o log detalhado para mais informações.";
            ShowBanner($"Falha na extração: {ex.Message}");
            Log($"Falha na extração: {ex.Message}");
            
            // Mostra notificação Android de erro se estiver no Android
            if (OperatingSystem.IsAndroid())
            {
                var top = TopLevel.GetTopLevel(this);
                if (top != null)
                {
                    OrbisExtract.App.Shared.AppNotificationHelper.ShowError(ex.Message);
                }
            }
        }
        finally
        {
            if (token.IsCancellationRequested && OperatingSystem.IsAndroid())
                OrbisExtract.App.Shared.AppNotificationHelper.CancelExtraction();

            timer?.Stop();
            timer?.Dispose();
            _clock.Stop();
            _elapsed.Text = _clock.Elapsed.ToString(@"mm\:ss");
            _cancel.IsVisible = false;
            _run?.Dispose();
            _run = null;
            if (packageStream is not null) try { await packageStream.DisposeAsync(); } catch { }
            if (tempPath is not null) try { File.Delete(tempPath); } catch { }
            Log("Processo de extração finalizado.");
            _choosePackage.IsEnabled = _chooseDestination.IsEnabled = true;
            _extract.IsEnabled = _package is not null && _destination is not null;
        }
    }

    // Cria [nome_do_pkg]_extracted na mesma localização do PKG original.
    // Se não houver permissão ao lado do PKG, usa a pasta temporária do sistema.
    private string CreateFallbackRoot()
    {
        var fallbackName = $"{_package!.Name}_extracted";
        try
        {
            var packagePath = _package.TryGetLocalPath();
            var packageDirectory = packagePath is null ? null : Path.GetDirectoryName(packagePath);
            if (!string.IsNullOrEmpty(packageDirectory) && Directory.Exists(packageDirectory))
            {
                var root = Path.Combine(packageDirectory, fallbackName);
                Directory.CreateDirectory(root);
                return root;
            }
        }
        catch
        {
            // Sem permissão ao lado do PKG: usa a pasta temporária do sistema.
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), fallbackName);
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
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
            // Grava diretamente no arquivo final do SAF: evita copiar cada arquivo extraído
            // novamente do temporário para o destino, o que era muito lento em PKGs grandes.
            var file = await targetFolder.CreateFileAsync(segments[^1]);
            if (file is null)
                throw new PkgExtractionException($"O provedor de armazenamento não criou o arquivo de destino para '{relativePath}'.");
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
            // O arquivo já é o destino final; não faça uma segunda cópia de seus bytes.
        }

        public async Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default)
        {
            if (_files.Remove(temporaryFile, out var file))
            {
                try { await temporaryFile.DisposeAsync(); } catch { }
                await file.DeleteAsync();
            }
        }
    }

    private sealed class LocalExtractionDestination : IExtractionDestination
    {
        private readonly string _root;
        private readonly bool _failOnExistingFiles;

        public LocalExtractionDestination(string root, bool failOnExistingFiles = true)
        {
            _root = Path.GetFullPath(root);
            _failOnExistingFiles = failOnExistingFiles;
            Directory.CreateDirectory(_root);
        }

        public Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = SafeExtractionPath.Resolve(_root, relativePath);
            if (_failOnExistingFiles && File.Exists(output))
                throw new PkgExtractionException($"O arquivo já existe: {output}");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var temporary = output + ".orbisextract.tmp";
            return Task.FromResult<Stream>(new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true));
        }

        public Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (temporaryFile is not FileStream file)
                throw new PkgExtractionException("Destino local recebeu um temporário inválido.");
            var output = SafeExtractionPath.Resolve(_root, relativePath);
            file.Flush();
            file.Dispose();
            File.Move(file.Name, output, overwrite: false);
            return Task.CompletedTask;
        }

        public Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default)
        {
            if (temporaryFile is FileStream file)
            {
                var name = file.Name;
                file.Dispose();
                if (File.Exists(name)) File.Delete(name);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class WindowsFallbackDestination : IExtractionDestination
    {
        private readonly string _root;
        private readonly string _packageName;
        private readonly Action<string> _log;

        public WindowsFallbackDestination(string root, string packageName, Action<string> log)
        {
            _root = Path.GetFullPath(root);
            _packageName = packageName;
            _log = log ?? (_ => { });
        }

        public Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = SafeExtractionPath.Resolve(_root, relativePath);
            var temporary = Path.Combine(Path.GetDirectoryName(output)!, $"{Path.GetFileNameWithoutExtension(output)}.orbisextract.tmp");
            _log($"Criando arquivo temporário: {temporary}");
            Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
            return Task.FromResult<Stream>(new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true));
        }

        public Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = SafeExtractionPath.Resolve(_root, relativePath);
            ((FileStream)temporaryFile).Flush();
            ((FileStream)temporaryFile).Dispose();
            if (File.Exists(((FileStream)temporaryFile).Name))
                File.Move(((FileStream)temporaryFile).Name, output, overwrite: false);
            return Task.CompletedTask;
        }

        public Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default)
        {
            if (temporaryFile is FileStream file)
            {
                var name = file.Name;
                file.Dispose();
                if (File.Exists(name)) File.Delete(name);
            }
            return Task.CompletedTask;
        }
    }
}
