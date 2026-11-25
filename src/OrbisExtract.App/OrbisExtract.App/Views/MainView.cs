using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OrbisExtract.Core;
using OrbisExtract.Infrastructure.OpenOrbis;
using OrbisExtract.App.Updates;
using OrbisExtract.App.Localization;
using OrbisExtract.App.Shared;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Button = Avalonia.Controls.Button;
using ProgressBar = Avalonia.Controls.ProgressBar;

namespace OrbisExtract.App.Views;

public sealed class MainView : UserControl
{
    private const int MaxLogLines = 250;
    private const string RepositoryUrl = "https://github.com/flugeru/OrbisExtract";

    // Pacote
    private readonly TextBlock _fileName;
    private readonly WrapPanel _chips;
    private readonly StackPanel _infoRows;
    private readonly TextBlock _gameTitle, _version, _contentId;
    private readonly Image _artwork;
    private readonly Border _artworkFrame;
    private readonly TextBlock _artworkStatus, _artworkCredit;
    private readonly Button _artworkSource, _artworkLicense;
    private readonly CommonsArtworkSearch _onlineArtwork = new();
    private Uri? _artworkSourceUri, _artworkLicenseUri;
    private readonly ComboBox _previewPicker;
    private readonly Button _choosePackage;

    // Configuração
    private readonly TextBlock _destinationName;
    private readonly Button _chooseDestination;
    private readonly Button _extract;
    private readonly CheckBox _resumeExisting;
    private readonly CheckBox _verifyPackage;

    // Monitor
    private readonly TextBlock _stateTitle, _counter, _percent, _elapsed, _status;
    private readonly ProgressBar _progress;
    private readonly Button _cancel, _pause;
    private readonly StackPanel _logPanel;
    private readonly ScrollViewer _logScroll;
    private readonly Border _banner;
    private readonly TextBlock _bannerText;
    private readonly Stopwatch _clock = new();

    // Layout
    private readonly Grid _contentGrid;
    private readonly Control _packageCard, _settingsCard, _monitorCard;
    private int _contentColumns;
    private readonly Border _aboutOverlay;
    private readonly Border _historyOverlay;
    private readonly StackPanel _historyList = new() { Spacing = 10 };
    private readonly Border _languageOverlay;
    private readonly Border _updateOverlay;
    private readonly Border _standaloneUpdateOverlay;
    private readonly ScrollViewer _mainPage;
    private readonly Button _standaloneUpdateCancel;
    private TaskCompletionSource<bool>? _standaloneUpdateDecision;
    private readonly TextBlock _updateMessage;
    private readonly Button _updateOpen;
    private Button _updateDismiss = null!;
    private TextBlock _aboutChangelog = null!;
    private TextBlock _repositoryError = null!;
    private string? _updateUrl;
    private bool _startupUpdateChecked;

    // Estado
    private IReadOnlyList<IStorageFile> _packages = [];
    private CancellationTokenSource? _previewRequest;
    private bool _selecting;
    private IStorageFolder? _destination;
    private CancellationTokenSource? _run;
    private ExtractionPauseGate? _pauseGate;
    private bool _extractionPaused;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Queue<Control> _logLines = new();
    private readonly List<string> _logMessages = new();
    private readonly List<string> _sessionEvents = new();
    private bool _recordingHistory;
    private readonly List<(TextBox Box, string Original)> _logEntries = new();
    private readonly Dictionary<TextBlock, string> _localizedLabels = new();
    private readonly Dictionary<Button, string> _localizedButtons = new();
    private readonly Dictionary<Button, (string Mode, string Label, TextBlock Name, TextBlock Check)> _languageChoices = new();
    private readonly Dictionary<TextBlock, Func<string>> _dynamicLabels = new();

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
        _artwork = new Image
        {
            Height = 132, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center
        };
        _artworkFrame = new Border
        {
            Child = _artwork,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = UiTheme.Brush(UiTheme.SurfaceElevated),
            BorderBrush = UiTheme.Brush(UiTheme.Primary),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(UiTheme.ControlRadius),
            ClipToBounds = true,
            IsVisible = false
        };
        _artworkStatus = UiTheme.Text("", 12, UiTheme.TextSecondary);
        _artworkStatus.IsVisible = false;
        _artworkCredit = UiTheme.Text("", 12, UiTheme.TextSecondary);
        _artworkCredit.TextWrapping = TextWrapping.Wrap;
        _artworkCredit.IsVisible = false;
        _artworkSource = UiTheme.OutlinedButton("Origem da imagem");
        _artworkLicense = UiTheme.OutlinedButton("Licença da imagem");
        _artworkSource.IsVisible = _artworkLicense.IsVisible = false;
        _artworkSource.Click += async (_, _) => await OpenArtworkLinkAsync(_artworkSourceUri);
        _artworkLicense.Click += async (_, _) => await OpenArtworkLinkAsync(_artworkLicenseUri);
        _previewPicker = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, IsVisible = false };
        _previewPicker.Classes.Add("pkgPicker");
        _previewPicker.SelectionChanged += async (_, _) =>
        {
            if (_previewPicker.SelectedIndex >= 0 && _packages.Count > 1)
                await PreviewPackageAsync(_previewPicker.SelectedIndex);
        };
        _infoRows = new StackPanel
        {
            Spacing = 10,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
            Children =
            {
                _artworkFrame, _artworkStatus, _artworkCredit, _artworkSource, _artworkLicense,
                Field("Jogo", _gameTitle), Field("Versão", _version), Field("Content ID", _contentId)
            }
        };
        _choosePackage = UiTheme.ActionButton("Selecionar arquivos PKG");
        _choosePackage.Click += async (_, _) => await ChoosePackageAsync();

        _packageCard = Section("Pacotes PKG",
            "Selecione um ou mais PKGs, incluindo o jogo base e seus updates.",
            _fileName, _choosePackage, _previewPicker, _chips, _infoRows);

        // ---------- Cartão de configuração (passcode removido da UI) ----------
        _destinationName = UiTheme.Text("Nenhuma pasta escolhida", 14, UiTheme.TextSecondary);
        _chooseDestination = UiTheme.ActionButton("Escolher pasta");
        _chooseDestination.Click += async (_, _) => await ChooseDestinationAsync();
        _extract = UiTheme.ActionButton("Extrair arquivos", primary: true);
        _extract.IsEnabled = false;
        _extract.Margin = new Thickness(0, 6, 0, 0);
        _extract.Click += async (_, _) => await ExtractAsync();

        _resumeExisting = new CheckBox { IsVisible = false };
        _verifyPackage = new CheckBox();
        _settingsCard = Section("Configuração",
            "Defina onde salvar os arquivos extraídos.",
            Field("Pasta de destino", _destinationName), _chooseDestination,
            _resumeExisting, _verifyPackage, _extract);

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
            IsVisible = false,
            Minimum = 0,
            Maximum = 100,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Foreground = UiTheme.Brush(UiTheme.Progress),
            Background = UiTheme.Brush(UiTheme.Outline)
        };
        _pause = UiTheme.OutlinedButton("Pausar");
        _pause.IsVisible = false;
        _pause.VerticalAlignment = VerticalAlignment.Center;
        _pause.Foreground = UiTheme.Brush(UiTheme.Primary);
        _pause.Click += (_, _) => TogglePause();
        _cancel = UiTheme.OutlinedButton("Cancelar");
        _cancel.IsVisible = false;
        _cancel.HorizontalAlignment = HorizontalAlignment.Right;
        _cancel.VerticalAlignment = VerticalAlignment.Center;
        _cancel.Margin = new Thickness(8, 0, 0, 0);
        _cancel.Foreground = UiTheme.Brush(UiTheme.Primary);
        _cancel.Click += (_, _) => _run?.Cancel();
        if (OperatingSystem.IsAndroid())
        {
            OrbisExtract.App.Shared.AppNotificationHelper.CancellationRequested += CancelFromNotification;
            OrbisExtract.App.Shared.AppNotificationHelper.PauseRequested += PauseFromNotification;
            OrbisExtract.App.Shared.AppNotificationHelper.TogglePauseRequested += TogglePauseFromNotification;
            OrbisExtract.App.Shared.AppNotificationHelper.ResumeRequested += ResumeFromNotification;
        }

        _logPanel = new StackPanel { Spacing = 0 };
        _logScroll = new LogScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _logPanel
        };
        var logBox = new Border
        {
            Background = UiTheme.Brush(UiTheme.SurfaceElevated),
            BorderBrush = UiTheme.Brush(UiTheme.Outline),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiTheme.ControlRadius),
            Padding = new Thickness(12),
            MinHeight = 112,
            Child = _logScroll
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

        // Wrap the extraction controls on narrow phones rather than compressing the
        // file counter and elapsed time into an unreadable single row.
        var metaRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
        metaRow.SizeChanged += (_, e) =>
        {
            var compact = e.NewSize.Width < 380;
            metaRow.RowDefinitions = new RowDefinitions(compact ? "Auto,Auto" : "Auto");
            metaRow.ColumnDefinitions = new ColumnDefinitions(compact ? "*,Auto,Auto" : "*,Auto,Auto,Auto");
            Grid.SetRow(_counter, 0);
            Grid.SetRow(_elapsed, compact ? 1 : 0);
            Grid.SetRow(_pause, compact ? 1 : 0);
            Grid.SetRow(_cancel, compact ? 1 : 0);
            Grid.SetColumn(_elapsed, compact ? 0 : 1);
            Grid.SetColumn(_pause, compact ? 1 : 2);
            Grid.SetColumn(_cancel, compact ? 2 : 3);
        };
        metaRow.Children.Add(_counter);
        Grid.SetColumn(_elapsed, 1);
        metaRow.Children.Add(_elapsed);
        Grid.SetColumn(_pause, 2);
        metaRow.Children.Add(_pause);
        Grid.SetColumn(_cancel, 3);
        metaRow.Children.Add(_cancel);

        var monitor = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10 };
        monitor.Children.Add(new StackPanel
        {
            Spacing = 10,
            Children = { header, _progress, metaRow, _status, _banner }
        });
        Grid.SetRow(logBox, 1);
        monitor.Children.Add(logBox);
        _monitorCard = UiTheme.Card(monitor);

        // ---------- Grid responsivo do conteúdo ----------
        _contentGrid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _contentGrid.Children.Add(_packageCard);
        _contentGrid.Children.Add(_settingsCard);
        _contentGrid.Children.Add(_monitorCard);

        var mainGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*") };
        _aboutOverlay = CreateAboutOverlay();
        _historyOverlay = CreateHistoryOverlay();
        _updateOverlay = CreateUpdateOverlay(out _updateMessage, out _updateOpen);
        var isAndroid = OperatingSystem.IsAndroid();
        var heading = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                UiTheme.Text("Extrair PKG", isAndroid ? 28 : 32, UiTheme.TextPrimary, FontWeight.SemiBold),
                UiTheme.Text("Ferramenta de extração para pacotes PlayStation 4.", 14, UiTheme.TextSecondary)
            }
        };
        var headerActions = CreateHeaderActions();
        var pageHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        pageHeader.Children.Add(heading);
        pageHeader.Children.Add(headerActions);
        var page = new StackPanel
        {
            Spacing = 18,
            Margin = isAndroid ? new Thickness(14, 14, 14, 18) : new Thickness(28, 28, 28, 24),
            Children =
            {
                pageHeader,
                _contentGrid,
            }
        };
        var centered = new Border { MaxWidth = isAndroid ? 820 : 1240, HorizontalAlignment = HorizontalAlignment.Stretch, Child = page };
        var scroll = _mainPage = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = centered
        };
        scroll.SizeChanged += (_, e) =>
        {
            ApplyContentLayout(e.NewSize.Width);
            // On narrow phones keep the subtitle full-width below the action icons.
            var narrow = e.NewSize.Width < 520;
            pageHeader.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,Auto");
            pageHeader.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "Auto");
            Grid.SetRow(headerActions, 0);
            Grid.SetColumn(headerActions, narrow ? 0 : 1);
            Grid.SetRow(heading, narrow ? 1 : 0);
            Grid.SetColumn(heading, 0);
            headerActions.HorizontalAlignment = narrow ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
        };

        // ---------- Layout principal (coluna única simplificada) ----------
        var wrapper = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        mainGrid.Children.Add(scroll);
        Grid.SetRow(scroll, 0);
        mainGrid.Children.Add(_aboutOverlay); // Add last so the modal overlay is rendered above the page.
        mainGrid.Children.Add(_historyOverlay);
        _languageOverlay = CreateLanguageOverlay();
        mainGrid.Children.Add(_languageOverlay);
        mainGrid.Children.Add(_updateOverlay);
        _standaloneUpdateOverlay = CreateStandaloneUpdateOverlay(out _standaloneUpdateCancel);
        mainGrid.Children.Add(_standaloneUpdateOverlay);
        wrapper.Children.Add(mainGrid);
        Content = wrapper;

        foreach (var label in this.GetLogicalDescendants().OfType<TextBlock>())
            if (label.Text is { } text) _localizedLabels[label] = text;
        foreach (var label in new[] { _fileName, _destinationName, _gameTitle, _version, _contentId, _stateTitle, _counter, _status, _bannerText, _updateMessage, _aboutChangelog })
            _localizedLabels.Remove(label);
        foreach (var button in this.GetLogicalDescendants().OfType<Button>())
            if (button.Content is string text) _localizedButtons[button] = text;
        AppLanguage.Changed += ApplyLanguage;
        DetachedFromVisualTree += (_, _) =>
        {
            AppLanguage.Changed -= ApplyLanguage;
            _standaloneUpdateDecision?.TrySetResult(false);
            if (OperatingSystem.IsAndroid())
            {
                OrbisExtract.App.Shared.AppNotificationHelper.CancellationRequested -= CancelFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.PauseRequested -= PauseFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.TogglePauseRequested -= TogglePauseFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.ResumeRequested -= ResumeFromNotification;
            }
        };
        AttachedToVisualTree += (_, _) =>
        {
            AppLanguage.Changed -= ApplyLanguage;
            AppLanguage.Changed += ApplyLanguage;
            if (OperatingSystem.IsAndroid())
            {
                OrbisExtract.App.Shared.AppNotificationHelper.CancellationRequested -= CancelFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.PauseRequested -= PauseFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.TogglePauseRequested -= TogglePauseFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.ResumeRequested -= ResumeFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.CancellationRequested += CancelFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.PauseRequested += PauseFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.TogglePauseRequested += TogglePauseFromNotification;
                OrbisExtract.App.Shared.AppNotificationHelper.ResumeRequested += ResumeFromNotification;
            }
        };
        ApplyLanguage();
        SetLocalized(_fileName, () => AppLanguage.Text("Nenhum pacote selecionado"));
        SetLocalized(_destinationName, () => AppLanguage.Text("Nenhuma pasta escolhida"));
        SetLocalized(_stateTitle, () => AppLanguage.Text("Pronto para extrair"));
        SetLocalized(_counter, () => AppLanguage.Text("Aguardando seleção de arquivo e destino."));
        SetLocalized(_aboutChangelog, () => AppLanguage.Text("• Configurações de idioma: automático, português ou inglês.\n• Idioma inicial de acordo com o sistema; escolha manual salva no aparelho."));

        ApplyContentLayout(isAndroid ? 400 : 900);
        SetProgressIdle();
        Log("[INFO]  OrbisExtract iniciado.");
        AttachedToVisualTree += (_, _) =>
        {
            if (_startupUpdateChecked) return;
            _startupUpdateChecked = true;
            _ = CheckForUpdatesAsync(false);
        };
    }

    private StackPanel CreateHeaderActions()
    {
        var settings = CreateRoundHeaderButton(true, "Configurações");
        settings.Click += (_, _) => _languageOverlay.IsVisible = true;
        var about = CreateRoundHeaderButton(false, "Sobre");
        about.Click += (_, _) => _aboutOverlay.IsVisible = true;
        var history = CreateRoundHeaderButton(false, "Histórico", true);
        history.Click += (_, _) => { RefreshHistory(); _historyOverlay.IsVisible = true; };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { history, settings, about } };
        return actions;
    }

    private static Button CreateRoundHeaderButton(bool settings, string tip, bool history = false)
    {
        // Both actions use the same outlined icon stroke, rather than mixing font glyphs.
        var icon = new Grid { Width = 24, Height = 24 };
        void Line(string data) => icon.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data), Stroke = UiTheme.Brush(UiTheme.Primary), StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round
        });
        if (history)
        {
            Line("M4 5 L4 10 L9 10 M4 10 A8 8 0 1 1 6 17 M12 7 L12 12 L16 14");
        }
        else if (settings)
        {
            // Sliders are used as a clear, lightweight settings symbol.
            Line("M4 7 L9 7 M15 7 L20 7 M4 17 L9 17 M15 17 L20 17");
            Line("M12 4 A3 3 0 1 1 12 10 A3 3 0 1 1 12 4 M12 14 A3 3 0 1 1 12 20 A3 3 0 1 1 12 14");
        }
        else
        {
            Line("M12 3 A9 9 0 1 1 12 21 A9 9 0 1 1 12 3 M12 11 L12 16");
            icon.Children.Add(new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 2, Height = 2, Fill = UiTheme.Brush(UiTheme.Primary),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 7, 0, 0)
            });
        }
        var button = UiTheme.OutlinedButton("");
        button.Content = icon;
        button.Width = 44;
        button.Height = 44;
        button.MinWidth = 44;
        button.MinHeight = 44;
        button.Padding = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.HorizontalAlignment = HorizontalAlignment.Center;
        button.VerticalAlignment = VerticalAlignment.Center;
        button.CornerRadius = new CornerRadius(22);
        ToolTip.SetTip(button, tip);
        return button;
    }

    private Border CreateHistoryOverlay()
    {
        var close = UiTheme.ActionButton("Fechar");
        var heading = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                UiTheme.Text("Histórico de extrações", 22, UiTheme.TextPrimary, FontWeight.SemiBold),
                UiTheme.Text("Relatórios locais por pacote PKG", 13, UiTheme.TextSecondary)
            }
        };
        var listContent = new StackPanel
        {
            Spacing = 16,
            Children = { heading, _historyList }
        };
        // Match the About dialog: one padded card, a scrollable body and a
        // separate fixed footer. Do not let the report cards touch the frame.
        var scroll = new ScrollViewer
        {
            Content = listContent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var layout = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 14 };
        layout.Children.Add(scroll);
        Grid.SetRow(close, 1);
        layout.Children.Add(close);

        var dialogCard = new Border
        {
            MaxWidth = 560,
            MaxHeight = 600,
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(UiTheme.CardRadius),
            ClipToBounds = true,
            Child = UiTheme.Card(layout, 20)
        };
        var overlayGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*"),
            ColumnDefinitions = new ColumnDefinitions("*")
        };
        overlayGrid.Children.Add(dialogCard);
        overlayGrid.SizeChanged += (_, e) =>
        {
            dialogCard.MaxWidth = Math.Min(560, Math.Max(0, e.NewSize.Width - 24));
            dialogCard.MaxHeight = Math.Min(600, Math.Max(0, e.NewSize.Height - 24));
        };
        var overlay = new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.Parse("#CC000000")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = overlayGrid
        };
        close.Click += (_, _) => overlay.IsVisible = false;
        return overlay;
    }

    private void RefreshHistory()
    {
        _historyList.Children.Clear();
        var entries = ExtractionHistory.Load();
        if (entries.Count == 0)
        {
            _historyList.Children.Add(UiTheme.Text(AppLanguage.Text("Nenhuma extração registrada."), 14, UiTheme.TextSecondary));
            return;
        }
        foreach (var entry in entries)
        {
            var title = UiTheme.Text(entry.PackageName, 15, UiTheme.TextPrimary, FontWeight.SemiBold);
            title.TextWrapping = TextWrapping.Wrap;
            var subtitle = UiTheme.Text(
                $"{entry.StartedAt.ToLocalTime():dd/MM/yyyy HH:mm} · {AppLanguage.Text(entry.Result)} · {TimeSpan.FromSeconds(entry.ElapsedSeconds):hh\\:mm\\:ss}",
                12, UiTheme.Primary);
            subtitle.TextWrapping = TextWrapping.Wrap;
            var details = UiTheme.Text(
                $"{AppLanguage.Text("Destino:")} {entry.Destination}\n{entry.Files:N0} {AppLanguage.Text("arquivos")} · {entry.Bytes / 1024d / 1024d:N1} MiB",
                12, UiTheme.TextSecondary);
            details.TextWrapping = TextWrapping.Wrap;
            var eventPanel = new StackPanel { Spacing = 4, IsVisible = false };
            foreach (var item in entry.Events)
            {
                var line = UiTheme.Text(item, 11, UiTheme.TextSecondary);
                line.TextWrapping = TextWrapping.Wrap;
                eventPanel.Children.Add(line);
            }
            var toggle = UiTheme.OutlinedButton(AppLanguage.Text("Ver relatório"));
            toggle.HorizontalAlignment = HorizontalAlignment.Stretch;
            toggle.Click += (_, _) => eventPanel.IsVisible = !eventPanel.IsVisible;
            _historyList.Children.Add(UiTheme.Card(new StackPanel
            {
                Spacing = 8,
                Children = { title, subtitle, details, toggle, eventPanel }
            }));
        }
    }

    private Border CreateLanguageOverlay()
    {
        var options = new StackPanel { Spacing = 8 };
        foreach (var (mode, label) in new[] { ("system", "Automático (sistema)"), ("pt", "Português"), ("en", "Inglês") })
        {
            var name = UiTheme.Text(label, 15);
            var check = UiTheme.Text("✓", 17, UiTheme.Primary, FontWeight.SemiBold);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(name);
            Grid.SetColumn(check, 1);
            row.Children.Add(check);
            var choice = UiTheme.OutlinedButton("");
            choice.Content = row;
            choice.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            choice.Padding = new Thickness(16, 12);
            _languageChoices[choice] = (mode, label, name, check);
            choice.Click += (_, _) =>
            {
                try { AppLanguage.SetMode(mode); _languageOverlay.IsVisible = false; }
                catch (Exception ex) { ShowBanner(ex.Message, UiTheme.Warning); }
            };
            options.Children.Add(choice);
        }
        var close = UiTheme.ActionButton("Fechar");
        close.Click += (_, _) => _languageOverlay.IsVisible = false;
        var card = UiTheme.Card(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Spacing = 16,
                Children = { UiTheme.Text("Configurações", 22, UiTheme.TextPrimary, FontWeight.SemiBold), UiTheme.Text("Idioma", 15, UiTheme.TextSecondary), options, close }
            }
        });
        card.Width = 440;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        var overlay = new Border { IsVisible = false, Background = new SolidColorBrush(Color.Parse("#CC000000")), Child = card };
        overlay.SizeChanged += (_, e) =>
        {
            card.Width = Math.Min(440, Math.Max(0, e.NewSize.Width - 32));
            card.MaxHeight = Math.Max(0, e.NewSize.Height - 32);
        };
        return overlay;
    }

    private void ApplyLanguage()
    {
        foreach (var (label, original) in _localizedLabels) label.Text = AppLanguage.Message(original);
        foreach (var (button, original) in _localizedButtons) button.Content = AppLanguage.Text(original);
        _resumeExisting.Content = AppLanguage.Text("Retomar e verificar arquivos existentes");
        _verifyPackage.Content = AppLanguage.Text("Verificar SHA-256 do PKG antes de extrair");
        if (_pauseGate is not null) _pause.Content = AppLanguage.Text(_extractionPaused ? "Retomar" : "Pausar");
        foreach (var (button, (mode, label, name, check)) in _languageChoices)
        {
            var selected = AppLanguage.Mode == mode;
            name.Text = AppLanguage.Text(label);
            name.Foreground = UiTheme.Brush(selected ? UiTheme.TextPrimary : UiTheme.TextSecondary);
            check.IsVisible = selected;
            button.Background = selected ? UiTheme.Brush(UiTheme.PrimaryContainer) : UiTheme.Brush(UiTheme.Surface);
            button.BorderBrush = UiTheme.Brush(selected ? UiTheme.Primary : UiTheme.Outline);
        }
        foreach (var (label, value) in _dynamicLabels) label.Text = value();
        foreach (var (box, original) in _logEntries) box.Text = LocalizeLog(original);
        foreach (var button in this.GetLogicalDescendants().OfType<Button>())
            if (ToolTip.GetTip(button) is string tip && tip is ("Sobre" or "Configurações" or "About" or "Settings"))
                ToolTip.SetTip(button, AppLanguage.Text(tip is "About" ? "Sobre" : tip is "Settings" ? "Configurações" : tip));
    }

    private void SetLocalized(TextBlock label, Func<string> value)
    {
        _dynamicLabels[label] = value;
        label.Text = value();
    }

    private void SetLiteral(TextBlock label, string value)
    {
        _dynamicLabels.Remove(label);
        label.Text = value;
    }

    private Border CreateAboutOverlay()
    {
        var licenseText = UiTheme.Text(AppLicense.Current, 12, UiTheme.TextSecondary);
        licenseText.TextWrapping = TextWrapping.Wrap;
        _dynamicLabels[licenseText] = () => AppLicense.Current;
        var close = UiTheme.ActionButton("Fechar");
        var licenseBox = new Border
        {
            Background = UiTheme.Brush(UiTheme.SurfaceElevated),
            BorderBrush = UiTheme.Brush(UiTheme.Outline),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiTheme.CardRadius),
            Padding = new Thickness(14),
            // The About dialog already has a parent ScrollViewer. A second,
            // fixed-height viewer here clips glyphs against the rounded border
            // whenever its content scrolls. Let the parent scroll the whole
            // license card instead, so text never passes behind its corners.
            Child = licenseText
        };
        _aboutChangelog = UiTheme.Text("• Configurações de idioma: automático, português ou inglês.\n• Idioma inicial de acordo com o sistema; escolha manual salva no aparelho.", 13, UiTheme.TextSecondary);
        _aboutChangelog.TextWrapping = TextWrapping.Wrap;
        var repository = new Button
        {
            Content = UiTheme.Text(RepositoryUrl, 13, UiTheme.Primary),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        _repositoryError = UiTheme.Text("", 13, UiTheme.Error);
        _repositoryError.IsVisible = false;
        repository.Click += (_, _) =>
        {
            try
            {
                if (OperatingSystem.IsAndroid())
                    (UpdateChecker.OpenAndroidUrl ?? throw new InvalidOperationException(AppLanguage.Text("Navegador indisponível.")))(RepositoryUrl);
                else
                    Process.Start(new ProcessStartInfo(RepositoryUrl) { UseShellExecute = true });
                _repositoryError.IsVisible = false;
            }
            catch (Exception ex)
            {
                SetLocalized(_repositoryError, () => $"{AppLanguage.Text("Não foi possível abrir o link:")} {ex.Message}");
                _repositoryError.IsVisible = true;
            }
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        UiTheme.Text("Sobre o OrbisExtract", 22, UiTheme.TextPrimary, FontWeight.SemiBold),
                        UiTheme.Text($"Versão {UpdateChecker.CurrentVersion}", 14, UiTheme.TextSecondary)
                    }
                },
                AboutSection("Repositório", repository, _repositoryError),
                AboutSection("Changelog", _aboutChangelog, CreateUpdateButton()),
                AboutSection("Licença GPL-3.0", licenseBox)
            }
        };
        close.Click += (_, _) => _aboutOverlay.IsVisible = false;
        close.Margin = new Thickness(0, 12, 0, 0);
        var body = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var dialogContent = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        dialogContent.Children.Add(body);
        Grid.SetRow(close, 1);
        dialogContent.Children.Add(close);
        var dialogCard = new Border
        {
            MaxWidth = 520,
            MaxHeight = 560,
            Margin = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(UiTheme.CardRadius),
            ClipToBounds = true,
            Child = UiTheme.Card(dialogContent, 20)
        };
        // A single flexible cell constrains the dialog to the actual viewport,
        // including narrow Android screens, instead of letting an Auto column overflow.
        var overlayGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*"),
            ColumnDefinitions = new ColumnDefinitions("*")
        };
        overlayGrid.Children.Add(dialogCard);
        overlayGrid.SizeChanged += (_, e) =>
        {
            dialogCard.MaxWidth = Math.Min(520, Math.Max(0, e.NewSize.Width - 24));
            dialogCard.MaxHeight = Math.Min(560, Math.Max(0, e.NewSize.Height - 24));
        };
        return new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.Parse("#CC000000")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = overlayGrid
        };
    }

    private static Border AboutSection(string title, params Control[] children)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(UiTheme.Text(title, 15, UiTheme.TextPrimary, FontWeight.SemiBold));
        foreach (var child in children) stack.Children.Add(child);
        return new Border
        {
            Padding = new Thickness(0, 4),
            Child = stack
        };
    }

    private Button CreateUpdateButton()
    {
        var button = UiTheme.OutlinedButton("Verificar atualizações");
        button.Click += async (_, _) => await CheckForUpdatesAsync(true);
        return button;
    }

    private Border CreateUpdateOverlay(out TextBlock message, out Button open)
    {
        message = UiTheme.Text("", 14, UiTheme.TextPrimary);
        message.TextWrapping = TextWrapping.Wrap;
        open = UiTheme.ActionButton("Abrir atualização", primary: true);
        open.Click += (_, _) =>
        {
            if (_updateUrl is not null)
            {
                try
                {
                    if (OperatingSystem.IsAndroid())
                        (UpdateChecker.OpenAndroidUrl ?? throw new InvalidOperationException(AppLanguage.Text("Navegador indisponível.")))(_updateUrl);
                    else
                        Process.Start(new ProcessStartInfo(_updateUrl) { UseShellExecute = true });
                }
                catch (Exception ex) { SetLocalized(_updateMessage, () => $"{AppLanguage.Text("Não foi possível abrir o link:")} {ex.Message}"); return; }
            }
            _updateOverlay.IsVisible = false;
        };
        var later = UiTheme.OutlinedButton("Agora não");
        _updateDismiss = later;
        later.Click += (_, _) => _updateOverlay.IsVisible = false;
        var card = UiTheme.Card(new ScrollViewer
        {
            MaxHeight = 440,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Spacing = 14,
                Children = { UiTheme.Text("Atualização do OrbisExtract", 20, UiTheme.TextPrimary, FontWeight.SemiBold), message, open, later }
            }
        });
        card.Width = 440;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        var overlay = new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.Parse("#CC000000")),
            Child = card
        };
        overlay.SizeChanged += (_, e) =>
        {
            card.Width = Math.Min(440, Math.Max(0, e.NewSize.Width - 32));
            card.MaxHeight = Math.Max(0, e.NewSize.Height - 32);
        };
        return overlay;
    }

    private Border CreateStandaloneUpdateOverlay(out Button cancel)
    {
        var title = UiTheme.Text("Extrair somente o update?", 20, UiTheme.TextPrimary, FontWeight.SemiBold);
        title.TextWrapping = TextWrapping.Wrap;
        var message = UiTheme.Text("Só os arquivos do update serão extraídos. Isso não instala a atualização nem gera o jogo completo.", 14, UiTheme.TextSecondary);
        message.TextWrapping = TextWrapping.Wrap;
        var confirm = UiTheme.ActionButton("Extrair só o update", primary: true);
        cancel = UiTheme.OutlinedButton("Cancelar");
        confirm.Click += (_, _) => _standaloneUpdateDecision?.TrySetResult(true);
        cancel.Click += (_, _) => _standaloneUpdateDecision?.TrySetResult(false);
        var card = UiTheme.Card(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Spacing = 14, Children = { title, message, confirm, cancel } }
        });
        card.MaxWidth = 440;
        card.Margin = new Thickness(16);
        card.VerticalAlignment = VerticalAlignment.Center;
        var overlay = new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.Parse("#CC000000")),
            Child = card
        };
        overlay.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            _standaloneUpdateDecision?.TrySetResult(false);
        };
        return overlay;
    }

    private async Task<bool> ConfirmStandaloneUpdateAsync()
    {
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _standaloneUpdateDecision = decision;
        _mainPage.IsEnabled = false;
        _standaloneUpdateOverlay.IsVisible = true;
        _standaloneUpdateCancel.Focus();
        try { return await decision.Task; }
        finally
        {
            _standaloneUpdateOverlay.IsVisible = false;
            _mainPage.IsEnabled = true;
            _standaloneUpdateDecision = null;
        }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        try
        {
            var update = await UpdateChecker.CheckAsync(_lifetime.Token);
            if (update is null)
            {
                if (manual) ShowUpdateMessage(() => AppLanguage.Text("Você já está na versão mais recente."), null);
                return;
            }
            if (update.ReleaseNotes.Count > 0 || update.ReleaseNotesEn is { Count: > 0 })
                SetLocalized(_aboutChangelog, () => $"{AppLanguage.Text("Nova versão")} {update.Version}:\n" + string.Join("\n", update.NotesForCurrentLanguage.Select(note => "• " + note)));
            ShowUpdateMessage(() => $"{AppLanguage.Text("Versão")} {update.Version} {AppLanguage.Text("disponível.")}" +
                (update.NotesForCurrentLanguage.Count == 0 ? "" : $"\n\n{AppLanguage.Text("Novidades:")}\n" + string.Join("\n", update.NotesForCurrentLanguage.Select(note => "• " + note))) +
                $"\n\n{AppLanguage.Text("Deseja abrir a página para baixar a atualização?")}", update.Url);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (manual) ShowUpdateMessage(() => $"{AppLanguage.Text("Não foi possível verificar atualizações:")} {ex.Message}", null);
        }
    }

    private void ShowUpdateMessage(Func<string> message, string? url)
    {
        _updateUrl = url;
        SetLocalized(_updateMessage, message);
        _updateOpen.IsVisible = url is not null;
        _localizedButtons[_updateDismiss] = url is null ? "Fechar" : "Agora não";
        _updateDismiss.Content = AppLanguage.Text(_localizedButtons[_updateDismiss]);
        _aboutOverlay.IsVisible = false;
        _updateOverlay.IsVisible = true;
    }

    // ===================== Layout =====================

    private void ApplyContentLayout(double width)
    {
        // Keep three cards side by side only when each has room for its controls and log.
        var columns = !OperatingSystem.IsAndroid() && width >= 1080 ? 3
            : !OperatingSystem.IsAndroid() && width >= 650 ? 2 : 1;
        if (_contentColumns == columns) return;
        _contentColumns = columns;

        _contentGrid.ColumnDefinitions = new ColumnDefinitions(columns == 3 ? "*,*,*" : columns == 2 ? "*,*" : "*");
        _contentGrid.RowDefinitions = new RowDefinitions(columns == 1 ? "Auto,Auto,Auto" : columns == 2 ? "Auto,Auto" : "Auto");

        // Android/narrow desktop: stacked. Medium desktop: two columns.
        // Wide desktop: package | destination/settings | extraction monitor.
        Place(_packageCard, 0, 0, 1);
        Place(_settingsCard, columns == 3 ? 0 : 1, columns == 3 ? 1 : 0, 1);
        Place(_monitorCard, columns == 1 ? 2 : 0, columns == 1 ? 0 : columns == 2 ? 1 : 2, 1);
        if (columns == 2) Grid.SetRowSpan(_monitorCard, 2);
        _packageCard.IsVisible = true;
        _settingsCard.IsVisible = true;
        _monitorCard.IsVisible = true;
    }

    private static void Place(Control c, int row, int col, int colSpan)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, col);
        Grid.SetColumnSpan(c, colSpan);
        Grid.SetRowSpan(c, 1);
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
        if (message is null) { _dynamicLabels.Remove(_bannerText); return; }
        SetBanner(() => AppLanguage.Message(message), color);
    }

    private void SetBanner(Func<string> message, string color)
    {
        _banner.IsVisible = true;
        SetLocalized(_bannerText, message);
        _banner.BorderBrush = UiTheme.Brush(color);
        _banner.Background = new SolidColorBrush(Color.Parse(color), 0.12);
    }

    // The page scrolls vertically, so the card may be measured with unbounded height.
    // Limit the log's requested height but let it stretch to the actual card height.
    private sealed class LogScrollViewer : ScrollViewer
    {
        protected override Type StyleKeyOverride => typeof(ScrollViewer);

        protected override Size MeasureOverride(Size availableSize) =>
            base.MeasureOverride(double.IsPositiveInfinity(availableSize.Height)
                ? new Size(availableSize.Width, 260)
                : availableSize);
    }

    private void SetProgressIdle()
    {
        _progress.IsIndeterminate = false;
        _progress.Value = 0;
        _percent.Text = "";
    }

    private static string LocalizeLog(string message)
    {
        // Keep severity prefixes intact while translating the actual message.
        // AppLanguage.Message expects the Portuguese text without [INFO]/[ERROR].
        var prefixEnd = message.IndexOf("]  ", StringComparison.Ordinal);
        if (message.StartsWith("[", StringComparison.Ordinal) && prefixEnd >= 0)
            return message[..(prefixEnd + 3)] + AppLanguage.Message(message[(prefixEnd + 3)..]);
        return AppLanguage.Message(message);
    }

    private void PauseFromNotification() => Dispatcher.UIThread.Post(PauseExtraction);

    private void PauseExtraction()
    {
        if (_run is null || _run.IsCancellationRequested || _pauseGate is null) return;
        if (_extractionPaused) return;
        _extractionPaused = true;
        _pauseGate.Pause();
        _clock.Stop();
        _pause.Content = AppLanguage.Text("Retomar");
        OrbisExtract.App.Shared.AppNotificationHelper.SetPaused(true);
        SetLocalized(_stateTitle, () => AppLanguage.Text("Pausado"));
    }

    private void ResumeFromNotification() => Dispatcher.UIThread.Post(ResumeExtraction);

    private void ResumeExtraction()
    {
        if (_run is null || _run.IsCancellationRequested || _pauseGate is null || !_extractionPaused) return;
        _extractionPaused = false;
        _pauseGate.Resume();
        _clock.Start();
        _pause.Content = AppLanguage.Text("Pausar");
        OrbisExtract.App.Shared.AppNotificationHelper.SetPaused(false);
        SetLocalized(_stateTitle, () => AppLanguage.Text("Extraindo"));
    }

    private void TogglePause()
    {
        if (_extractionPaused) ResumeExtraction();
        else PauseExtraction();
    }

    private void TogglePauseFromNotification() => Dispatcher.UIThread.Post(TogglePause);

    private void CancelFromNotification() => Dispatcher.UIThread.Post(() => _run?.Cancel());

    private List<string> SnapshotSessionEvents()
    {
        lock (_sessionEvents) return new List<string>(_sessionEvents);
    }

    private void Log(string message)
    {
        var time = $"{DateTime.Now:HH:mm:ss}";
        if (_recordingHistory)
        {
            lock (_sessionEvents)
            {
                if (_sessionEvents.Count < 2000) _sessionEvents.Add($"{time}  {message}");
            }
        }
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
            var logText = new TextBox
            {
                Text = LocalizeLog(message),
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
            };
            var details = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    UiTheme.Text(time, 10, UiTheme.TextSecondary),
                    logText
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
            _logEntries.Add((logText, message));
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines)
            {
                _logPanel.Children.Remove(_logLines.Dequeue());
                _logEntries.RemoveAt(0);
            }
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
        if (top is null || _run is not null || _selecting) return;
        _selecting = true;
        _choosePackage.IsEnabled = _chooseDestination.IsEnabled = _extract.IsEnabled = false;
        try { await SelectPackagesAsync(top); }
        finally
        {
            _selecting = false;
            _choosePackage.IsEnabled = _chooseDestination.IsEnabled = true;
            _extract.IsEnabled = _packages.Count > 0 && _destination is not null;
        }
    }

    private async Task SelectPackagesAsync(TopLevel top)
    {
        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = AppLanguage.Text("Selecionar pacote PlayStation PKG"),
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(AppLanguage.Text("Pacotes PKG")) { Patterns = ["*.pkg", "*.PKG"] },
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

        _packages = files.DistinctBy(file => file.Path).ToArray();
        _resumeExisting.IsChecked = false;
        _resumeExisting.IsVisible = _packages.Count == 1;
        var names = _packages.Select(file => file.Name).ToArray();
        _previewRequest?.Cancel();
        if (names.Length == 1) SetLiteral(_fileName, names[0]);
        else SetLocalized(_fileName, () => $"{names.Length} {AppLanguage.Text("PKGs selecionados")}");
        _fileName.Foreground = UiTheme.Brush(UiTheme.TextPrimary);
        SetLocalized(_counter, () => $"{names.Length} {AppLanguage.Text("PKGs selecionados")}");
        ShowBanner(null);
        Log($"{names.Length} {AppLanguage.Text("PKGs selecionados")}");
        _previewPicker.SelectedIndex = -1;
        _previewPicker.ItemsSource = names;
        _previewPicker.IsVisible = names.Length > 1;
        if (names.Length == 1) _ = PreviewPackageAsync(0);
        else _previewPicker.SelectedIndex = 0;
    }

    private async Task PreviewPackageAsync(int index)
    {
        var package = _packages[index];
        _previewRequest?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _previewRequest = request;
        var token = request.Token;
        _infoRows.IsVisible = false;
        _artworkFrame.IsVisible = false;
        _artworkStatus.IsVisible = _artworkCredit.IsVisible = _artworkSource.IsVisible = _artworkLicense.IsVisible = false;
        _artworkSourceUri = _artworkLicenseUri = null;
        if (_artwork.Source is Bitmap old)
        {
            _artwork.Source = null;
            old.Dispose();
        }
        _chips.Children.Clear();
        _chips.IsVisible = false;
        try
        {
            PackagePreview preview;
            await using (var stream = await package.OpenReadAsync())
                preview = await new OpenOrbisPkgMetadataReader().ReadPreviewAsync(stream, token);
            token.ThrowIfCancellationRequested();
            var info = preview.Metadata;
            _chips.Children.Add(UiTheme.Chip("PS4"));
            if (info.TitleId is not null) _chips.Children.Add(UiTheme.Chip(info.TitleId));
            if (info.Kind == PackageKind.BaseGame) _chips.Children.Add(UiTheme.Chip(AppLanguage.Text("Jogo base")));
            if (info.Kind is PackageKind.Update or PackageKind.DeltaUpdate) _chips.Children.Add(UiTheme.Chip("Update"));
            _chips.IsVisible = true;
            SetLiteral(_gameTitle, info.Title);
            SetLiteral(_version, info.Version);
            SetLiteral(_contentId, info.ContentId);
            _infoRows.IsVisible = true;
            if (preview.Artwork is { } png && await ShowArtworkAsync(png, preview.ArtworkWidth, token))
                return;

            if (info.Title == "Título não informado") return;
            SetLocalized(_artworkStatus, () => AppLanguage.Text("Buscando arte online…"));
            _artworkStatus.IsVisible = true;
            var online = await _onlineArtwork.FindAsync(info.Title, token);
            token.ThrowIfCancellationRequested();
            if (online is not null && await ShowArtworkAsync(online.Image, online.Width, token))
            {
                _artworkStatus.IsVisible = false;
                _artworkSourceUri = online.SourceUri;
                _artworkLicenseUri = online.LicenseUri;
                SetLiteral(_artworkCredit, string.IsNullOrEmpty(online.Author)
                    ? $"{online.Title} · {online.License}"
                    : $"{online.Title} · {online.Author} · {online.License}");
                _artworkCredit.IsVisible = _artworkSource.IsVisible = true;
                _artworkLicense.IsVisible = _artworkLicenseUri is not null;
            }
            else SetLocalized(_artworkStatus, () => AppLanguage.Text("Sem arte online disponível."));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            Log($"Aviso ao ler metadados: {ex.Message}");
        }
        catch (Exception) when (token.IsCancellationRequested) { }
        finally
        {
            if (_previewRequest == request) _previewRequest = null;
        }
    }

    private async Task<bool> ShowArtworkAsync(byte[] image, int width, CancellationToken token)
    {
        try
        {
            var bitmap = await Task.Run(() =>
            {
                using var data = new MemoryStream(image, writable: false);
                return Bitmap.DecodeToWidth(data, Math.Min(512, width));
            }, token);
            if (token.IsCancellationRequested)
            {
                bitmap.Dispose();
                token.ThrowIfCancellationRequested();
            }
            _artwork.Source = bitmap;
            _artworkFrame.IsVisible = true;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log($"Capa indisponível: {ex.Message}");
            return false;
        }
    }

    private async Task OpenArtworkLinkAsync(Uri? uri)
    {
        if (uri is null) return;
        try
        {
            if (TopLevel.GetTopLevel(this) is { } top && await top.Launcher.LaunchUriAsync(uri)) return;
            ShowBanner("Navegador indisponível.", UiTheme.Warning);
        }
        catch (Exception ex)
        {
            Log($"Não foi possível abrir o link: {ex.Message}");
            ShowBanner("Navegador indisponível.", UiTheme.Warning);
        }
    }

    private async Task ChooseDestinationAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || _run is not null || _selecting) return;

        IReadOnlyList<IStorageFolder> folders;
        try
        {
            folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = AppLanguage.Text("Escolher pasta para os arquivos extraídos"),
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
        SetLiteral(_destinationName, _destination.Name);
        _destinationName.Foreground = UiTheme.Brush(UiTheme.TextPrimary);
        _extract.IsEnabled = _packages.Count > 0;
        Log($"Destino selecionado: {_destination.Name}");
    }

    private async Task ExtractAsync()
    {
        if (_run is not null || _selecting || _packages.Count == 0 || _destination is null) return;

        var packages = _packages;
        var selectedFolder = _destination;
        _previewRequest?.Cancel();
        _artworkStatus.IsVisible = false;
        _previewPicker.IsEnabled = false;
        var localPath = OperatingSystem.IsAndroid() ? null : selectedFolder.TryGetLocalPath();
        bool resumeExisting = packages.Count == 1 && _resumeExisting.IsChecked == true;
        bool verifyPackage = _verifyPackage.IsChecked == true;
        _resumeExisting.IsEnabled = _verifyPackage.IsEnabled = false;
        _choosePackage.IsEnabled = _chooseDestination.IsEnabled = _extract.IsEnabled = false;
        _run = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var pauseGate = _pauseGate = new ExtractionPauseGate();
        _extractionPaused = false;
        var token = _run.Token;
        lock (_sessionEvents) _sessionEvents.Clear();
        _recordingHistory = true;
        var historyStartedAt = DateTimeOffset.Now;
        var batchName = packages.Count == 1 ? packages[0].Name : $"{packages.Count} PKGs";
        var historyResult = "Falha na extração";
        long historyFiles = 0, historyBytes = 0;
        bool declinedStandaloneUpdate = false;

        ShowBanner(null);
        _cancel.IsVisible = _pause.IsVisible = _elapsed.IsVisible = true;
        _status.IsVisible = false;
        _pause.Content = AppLanguage.Text("Pausar");
        SetLocalized(_stateTitle, () => AppLanguage.Text("Preparando"));
        SetLocalized(_counter, () => AppLanguage.Text("Preparando a extração…"));
        _progress.IsIndeterminate = true;
        _progress.IsVisible = true;
        _percent.Text = "";
        _elapsed.Text = "00:00";
        _clock.Restart();

        var progressSlots = Enumerable.Range(0, packages.Count).Select(_ => new PackageProgress()).ToArray();
        int progressTotal = 0;
        long lastNotificationSecond = -1;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) =>
        {
            var elapsed = _clock.Elapsed;
            _elapsed.Text = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
            if (progressTotal == 0) return;
            var snapshots = progressSlots.Select(slot => slot.Snapshot).ToArray();
            int done = snapshots.Count(snapshot => snapshot.Completed);
            int active = snapshots.Count(snapshot => snapshot.Active);
            double fraction = Math.Clamp(snapshots.Sum(snapshot => snapshot.Fraction) / progressTotal, 0, 1);
            string Detail() => $"{done:N0} {AppLanguage.Text("de")} {progressTotal:N0} PKGs · {active:N0} {AppLanguage.Text("em execução")}";
            _progress.IsIndeterminate = false;
            _progress.Value = fraction * 100;
            _percent.Text = $"{fraction:P0}";
            string stage = snapshots.Any(snapshot => snapshot.Verifying) ? "Verificando" : "Extraindo";
            if (!_extractionPaused) SetLocalized(_stateTitle, () => AppLanguage.Text(stage));
            SetLocalized(_counter, Detail);
            var second = (long)elapsed.TotalSeconds;
            if (lastNotificationSecond == second) return;
            lastNotificationSecond = second;
            AppNotificationHelper.UpdateElapsed(elapsed);
            AppNotificationHelper.UpdateProgress((long)(fraction * 1000), 1000, stage, Detail());
        };

        try
        {
            bool limitedIo = UsesConstrainedStorage(localPath);
            long estimatedWorkerBytes = 48 * 1024 * 1024;
            int WorkerLimit() => GetWorkerLimit(limitedIo, estimatedWorkerBytes);
            var (readable, unreadable) = await ReadBatchPackagesAsync(packages, pauseGate, WorkerLimit(), token);
            token.ThrowIfCancellationRequested();
            bool standaloneUpdate = packages.Count == 1 && readable is [{ Metadata.Kind: PackageKind.Update }];
            if (standaloneUpdate && !await ConfirmStandaloneUpdateAsync())
            {
                declinedStandaloneUpdate = true;
                SetProgressIdle();
                SetLocalized(_stateTitle, () => AppLanguage.Text("Pronto para extrair"));
                SetLiteral(_counter, "");
                return;
            }
            token.ThrowIfCancellationRequested();
            Log($"Iniciando extração. Pacote: {batchName}");
            Log($"Destino: {selectedFolder.Name}");
            var plan = BatchPlanner.Create(readable, allowStandaloneUpdate: standaloneUpdate);
            var issues = unreadable.Concat(plan.Issues).ToArray();
            foreach (var issue in issues)
                Log($"{issue.Package}: {AppLanguage.Message(issue.Reason)}");
            if (plan.Groups.Count == 0)
                throw new PkgExtractionException(issues.FirstOrDefault()?.Reason ?? "Nenhum PKG pôde ser preparado.");

            estimatedWorkerBytes = plan.Groups.Max(group =>
                group.Packages.Max(package => package.Metadata.EstimatedWorkingSetBytes));
            if (standaloneUpdate) Log(AppLanguage.Text("Extraindo somente os arquivos do update."));
            _clock.Restart();
            AppNotificationHelper.StartExtraction(batchName, selectedFolder.Name, 0, 0);
            timer.Start();
            progressTotal = plan.Groups.Sum(group => group.Packages.Count);
            var hardware = GetHardwareBudget();
            int workers = Math.Min(ParallelExtraction.WorkerLimit(hardware.Processors, hardware.Bytes, limitedIo,
                hardware.Pressure, estimatedWorkerBytes), plan.Groups.Count);
            Log($"{AppLanguage.Text("Grupos de extração")}: {plan.Groups.Count}; {AppLanguage.Text("operações simultâneas")}: {workers}.");
            Log($"CPU: {hardware.Processors}; {AppLanguage.Text("RAM para o lote")}: {FormatSize((ulong)hardware.Bytes)}; " +
                $"{AppLanguage.Text("estimativa por extração")}: {FormatSize((ulong)estimatedWorkerBytes)}.");
            bool directOutput = packages.Count == 1;
            // Spend the CPU budget inside one local package, or across groups, not both.
            int fileWorkers = plan.Groups.Count == 1 && !limitedIo && !hardware.Pressure
                ? Math.Clamp(Math.Min(hardware.Processors / 2, (int)Math.Min(4,
                    Math.Max(0, hardware.Bytes - estimatedWorkerBytes) / (16 * 1024 * 1024))), 1, 4)
                : 1;
            var results = await ParallelExtraction.RunAsync(plan.Groups, WorkerLimit, async (group, stop) =>
            {
                await pauseGate.WaitAsync(stop);
                await using var workspace = directOutput ? null : await CreateWorkspaceAsync(selectedFolder, localPath, group.OutputName, stop);
                var extractor = new OpenOrbisPkgExtractor();
                long files = 0, bytes = 0;
                foreach (var member in group.Packages)
                {
                    await pauseGate.WaitAsync(stop);
                    var slot = progressSlots[member.Index];
                    slot.Begin();
                    try
                    {
                        var destination = workspace?.GetDestination(member != group.Packages[0])
                            ?? CreateDirectDestination(selectedFolder, localPath);
                        Log($"{group.OutputName} · {(member == group.Packages[0] ? AppLanguage.Text("Extraindo PKG") : AppLanguage.Text("Aplicando update"))}: {member.Name}");
                        var result = await ExtractPackageAsync(packages[member.Index], member.Metadata,
                            destination, extractor, slot, pauseGate, fileWorkers, resumeExisting, verifyPackage, stop);
                        files += result.FilesExtracted;
                        bytes += result.BytesExtracted;
                        if (result.PerformanceSummary is not null) Log($"{member.Name} · {result.PerformanceSummary}");
                        slot.Finish();
                    }
                    catch
                    {
                        slot.Stop();
                        throw;
                    }
                }
                if (workspace is not null)
                {
                    await pauseGate.WaitAsync(stop);
                    await workspace.PublishAsync(stop);
                }
                Log($"{AppLanguage.Text("Grupo concluído")}: {(directOutput ? selectedFolder.Name : group.OutputName)}");
                return new ExtractionResult(files, bytes);
            }, token);

            historyFiles = results.Sum(result => result.Extraction?.FilesExtracted ?? 0);
            historyBytes = results.Sum(result => result.Extraction?.BytesExtracted ?? 0);
            foreach (var failure in results.Where(result => result.Error is not null and not OperationCanceledException))
                Log($"{failure.Item.OutputName}: {AppLanguage.Message(failure.Error!.Message)}");

            token.ThrowIfCancellationRequested();
            int failures = issues.Length + results.Count(result => result.Error is not null);
            if (results.All(result => result.Extraction is null))
                throw new PkgExtractionException(results.First(result => result.Error is not null).Error!.Message);

            historyResult = failures == 0 ? "Concluído" : "Concluído com falhas";
            _progress.IsIndeterminate = false;
            if (failures == 0)
            {
                _progress.Value = 100;
                _percent.Text = "100%";
            }
            SetLocalized(_stateTitle, () => AppLanguage.Text(historyResult));
            SetLocalized(_counter, () => $"{historyFiles:N0} {AppLanguage.Text("arquivos processados")} · {FormatSize((ulong)historyBytes)}");
            if (failures == 0)
            {
                var duration = _clock.Elapsed.ToString(@"hh\:mm\:ss");
                SetBanner(() => $"{AppLanguage.Text("Extração concluída com sucesso em")} {duration}.", UiTheme.Success);
                AppNotificationHelper.CompleteExtraction(historyFiles, historyBytes);
            }
            else
            {
                ShowBanner("Alguns PKGs não foram extraídos. Veja o log.", UiTheme.Warning);
                AppNotificationHelper.ShowError(AppLanguage.Text("Alguns PKGs não foram extraídos. Veja o log."));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            historyResult = "Cancelado";
            _progress.IsIndeterminate = false;
            SetLocalized(_stateTitle, () => AppLanguage.Text("Cancelado"));
            SetLocalized(_counter, () => AppLanguage.Text("A extração foi interrompida."));
            ShowBanner("Extração cancelada.", UiTheme.Primary);
            Log("Extração cancelada.");
            AppNotificationHelper.CancelExtraction();
        }
        catch (Exception ex)
        {
            _progress.IsIndeterminate = false;
            SetLocalized(_stateTitle, () => AppLanguage.Text("Falha na extração"));
            SetLocalized(_counter, () => AppLanguage.Text("Veja o log detalhado para mais informações."));
            SetBanner(() => $"{AppLanguage.Text("Falha na extração:")} {AppLanguage.Message(ex.Message)}", UiTheme.Error);
            Log($"Falha na extração: {ex.Message}");
            AppNotificationHelper.ShowError(AppLanguage.Message(ex.Message));
        }
        finally
        {
            timer.Stop();
            _progress.IsVisible = false;
            _percent.Text = "";
            _clock.Stop();
            _elapsed.Text = $"{(int)_clock.Elapsed.TotalMinutes:00}:{_clock.Elapsed.Seconds:00}";
            _cancel.IsVisible = _pause.IsVisible = false;
            _pauseGate = null;
            _extractionPaused = false;
            _run.Dispose();
            _run = null;
            _elapsed.IsVisible = !declinedStandaloneUpdate;
            if (!declinedStandaloneUpdate)
            {
                Log("Processo de extração finalizado.");
                ExtractionHistory.Add(new ExtractionHistoryEntry(
                    historyStartedAt, batchName, selectedFolder.Name, historyResult,
                    historyFiles, historyBytes, (long)_clock.Elapsed.TotalSeconds, SnapshotSessionEvents()));
            }
            _recordingHistory = false;
            _choosePackage.IsEnabled = _chooseDestination.IsEnabled = _extract.IsEnabled = true;
            _resumeExisting.IsEnabled = _verifyPackage.IsEnabled = true;
            _previewPicker.IsEnabled = true;
        }
    }

    private static async Task<(BatchPackage[] Readable, BatchIssue[] Unreadable)> ReadBatchPackagesAsync(
        IReadOnlyList<IStorageFile> packages, ExtractionPauseGate gate, int workers, CancellationToken token)
    {
        var inputs = new BatchPackage?[packages.Count];
        var errors = new BatchIssue?[packages.Count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = token };
        await Parallel.ForEachAsync(Enumerable.Range(0, packages.Count), options, async (index, ct) =>
        {
            var file = packages[index];
            await gate.WaitAsync(ct);
            try
            {
                await using var source = await file.OpenReadAsync();
                var metadata = await new OpenOrbisPkgMetadataReader().ReadAsync(source, ct);
                inputs[index] = new BatchPackage(index, file.Name, metadata);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors[index] = new BatchIssue(file.Name, ex.Message);
            }
        });
        return (inputs.OfType<BatchPackage>().ToArray(), errors.OfType<BatchIssue>().ToArray());
    }

    private async Task<ExtractionResult> ExtractPackageAsync(
        IStorageFile package, PackageMetadata expected, IExtractionDestination destination, IPkgExtractor extractor,
        IProgress<ExtractionProgress> progress, ExtractionPauseGate gate, int fileWorkers,
        bool resumeExisting, bool verifyPackage, CancellationToken token)
    {
        await using var original = await package.OpenReadAsync();
        async Task<ExtractionResult> ExtractStreamAsync(Stream input)
        {
            var current = await new OpenOrbisPkgMetadataReader().ReadAsync(input, token);
            if (current != expected)
                throw new PkgExtractionException($"Os metadados mudaram antes da extração: {package.Name}");
            return await extractor.ExtractAsync(new ExtractionRequest(input, string.Empty,
                Destination: destination, PauseGate: gate, MaxFileWorkers: fileWorkers,
                ResumeExistingFiles: resumeExisting, VerifyPackageIntegrity: verifyPackage), progress, token);
        }

        if (original.CanSeek)
        {
            if (!OperatingSystem.IsAndroid()) return await ExtractStreamAsync(original);
            await using var buffered = new BufferedStream(original, 256 * 1024);
            return await ExtractStreamAsync(buffered);
        }

        Log($"{package.Name}: {AppLanguage.Text("Preparando cópia temporária para leitura.")}");
        var tempPath = Path.Combine(Path.GetTempPath(), $"orbisextract-{Guid.NewGuid():N}.pkg");
        await using var staged = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
        try
        {
            while (true)
            {
                await gate.WaitAsync(token);
                int count = await original.ReadAsync(buffer.AsMemory(0, 1024 * 1024), token);
                if (count == 0) break;
                await staged.WriteAsync(buffer.AsMemory(0, count), token);
            }
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
        staged.Position = 0;
        return await ExtractStreamAsync(staged);
    }

    private IExtractionDestination CreateDirectDestination(IStorageFolder folder, string? localPath) =>
        localPath is null ? new StorageFolderExtractionDestination(folder, Log) : new LocalExtractionDestination(localPath);

    private async Task<IExtractionWorkspace> CreateWorkspaceAsync(
        IStorageFolder folder, string? localPath, string outputName, CancellationToken token)
    {
        if (localPath is not null) return new LocalExtractionWorkspace(localPath, outputName);
#if ANDROID
        return await StorageExtractionWorkspace.CreateAsync(folder, outputName, Log, token);
#else
        await Task.CompletedTask;
        throw new PkgExtractionException("Escolha uma pasta local ou de rede para extrair em lote.");
#endif
    }

    private static bool UsesConstrainedStorage(string? localPath)
    {
        if (localPath is null || localPath.StartsWith(@"\\", StringComparison.Ordinal)) return true;
        try { return new DriveInfo(Path.GetPathRoot(localPath)!).DriveType is DriveType.Network or DriveType.Removable; }
        catch (IOException) { return true; }
        catch (ArgumentException) { return true; }
    }

    private static int GetWorkerLimit(bool limitedIo, long estimatedWorkerBytes)
    {
        var hardware = GetHardwareBudget();
        return ParallelExtraction.WorkerLimit(hardware.Processors, hardware.Bytes, limitedIo, hardware.Pressure, estimatedWorkerBytes);
    }

    private static (int Processors, long Bytes, bool Pressure) GetHardwareBudget()
    {
        int processors = Environment.ProcessorCount;
#if ANDROID
        var manager = global::Android.App.Application.Context.GetSystemService(
            global::Android.Content.Context.ActivityService) as global::Android.App.ActivityManager;
        if (manager is not null)
        {
            using var available = new global::Android.App.ActivityManager.MemoryInfo();
            manager.GetMemoryInfo(available);
            // Android's current low-memory state includes the native and
            // managed parts of the app. GC counters can reflect an older GC
            // and only describe limits for the managed runtime.
            if (available.TotalMem > 0)
                return (processors, Math.Max(1, (available.AvailMem - available.Threshold) / 2), available.LowMemory);
        }
#endif
        var memory = GC.GetGCMemoryInfo();
        bool pressure = memory.HighMemoryLoadThresholdBytes > 0 &&
            memory.MemoryLoadBytes >= memory.HighMemoryLoadThresholdBytes;
        return (processors, Math.Max(0, memory.TotalAvailableMemoryBytes / 3), pressure);
    }

    private sealed record ProgressSnapshot(bool Active, bool Completed, double Fraction, bool Verifying = false);

    private sealed class PackageProgress : IProgress<ExtractionProgress>
    {
        private ProgressSnapshot _snapshot = new(false, false, 0);
        public ProgressSnapshot Snapshot => Volatile.Read(ref _snapshot);
        public void Begin() => Volatile.Write(ref _snapshot, new(true, false, 0));
        public void Finish() => Volatile.Write(ref _snapshot, new(false, true, 1));
        public void Stop() => Volatile.Write(ref _snapshot, new(false, false, 0));

        public void Report(ExtractionProgress value)
        {
            var fraction = value.BytesTotal is > 0 ? (double)value.BytesCompleted / value.BytesTotal.Value
                : value.FilesTotal is > 0 ? (double)value.FilesCompleted / value.FilesTotal.Value : Snapshot.Fraction;
            fraction = Math.Clamp(fraction, 0, 1);
            bool verifying = value.State == "Verificando";
            var previous = Snapshot;
            if (previous.Fraction != fraction || previous.Verifying != verifying)
                Volatile.Write(ref _snapshot, new(true, false, fraction, verifying));
        }
    }

#if ANDROID
    private sealed class StorageExtractionWorkspace : IExtractionWorkspace
    {
        private readonly IStorageFolder parent;
        private readonly IStorageFolder staging;
        private readonly string outputName;
        private readonly Action<string> log;
        private bool published;
        private global::Android.Net.Uri? stagingUri;

        private StorageExtractionWorkspace(IStorageFolder parent, IStorageFolder staging, string outputName, Action<string> log)
        {
            this.parent = parent;
            this.staging = staging;
            this.outputName = outputName;
            this.log = log;
        }

        public static async Task<StorageExtractionWorkspace> CreateAsync(
            IStorageFolder parent, string outputName, Action<string> log, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (await parent.GetFolderAsync(outputName) is not null || await parent.GetFileAsync(outputName) is not null)
                throw new PkgExtractionException($"O destino já existe: {outputName}");

            var name = $"orbisextract-{Guid.NewGuid():N}.partial";
            var folder = await parent.CreateFolderAsync(name)
                ?? throw new PkgExtractionException("O provedor não criou uma pasta temporária para o grupo.");
            var workspace = new StorageExtractionWorkspace(parent, folder, outputName, log);
            try
            {
                var resolver = global::Android.App.Application.Context.ContentResolver!;
                using var cursor = resolver.Query(workspace.TryGetStagingUri(),
                    [global::Android.Provider.DocumentsContract.Document.ColumnFlags], null, null, null);
                if (cursor is null || !cursor.MoveToFirst() ||
                    (cursor.GetLong(0) & (long)global::Android.Provider.DocumentContractFlags.SupportsRename) == 0)
                    throw new PkgExtractionException("Escolha um provedor de armazenamento que permita renomear pastas para extrair em lote.");
                token.ThrowIfCancellationRequested();
                return workspace;
            }
            catch
            {
                await workspace.DisposeAsync();
                throw;
            }
        }

        public IExtractionDestination GetDestination(bool overwrite) =>
            new StorageFolderExtractionDestination(staging, log, privateWorkspace: true, overwrite);

        public async Task PublishAsync(CancellationToken token)
        {
            if (await parent.GetFolderAsync(outputName) is not null || await parent.GetFileAsync(outputName) is not null)
                throw new PkgExtractionException($"O destino já existe: {outputName}");
            token.ThrowIfCancellationRequested();
            var resolver = global::Android.App.Application.Context.ContentResolver!;
            var renamed = global::Android.Provider.DocumentsContract.RenameDocument(resolver, TryGetStagingUri(), outputName);
            if (renamed is null)
                throw new PkgExtractionException("O provedor não publicou a pasta do grupo.");
            stagingUri = renamed;
            using var cursor = resolver.Query(renamed,
                [global::Android.Provider.DocumentsContract.Document.ColumnDisplayName], null, null, null);
            if (cursor is null || !cursor.MoveToFirst() || cursor.GetString(0) != outputName)
                throw new PkgExtractionException($"O provedor alterou o nome da pasta '{outputName}'.");
            published = true;
        }

        private global::Android.Net.Uri TryGetStagingUri() => stagingUri ??=
            global::Android.Net.Uri.Parse(staging.Path.OriginalString)
            ?? throw new PkgExtractionException("O provedor não retornou uma pasta temporária válida.");

        public ValueTask DisposeAsync()
        {
            try
            {
                if (!published && !global::Android.Provider.DocumentsContract.DeleteDocument(
                    global::Android.App.Application.Context.ContentResolver!, TryGetStagingUri()))
                    log($"Não foi possível remover a pasta temporária: {staging.Name}");
            }
            catch (Exception ex)
            {
                log($"Não foi possível remover a pasta temporária: {ex.Message}");
            }
            finally { staging.Dispose(); }
            return ValueTask.CompletedTask;
        }
    }
#endif

    private sealed class StorageFolderExtractionDestination : IResumableExtractionDestination
    {
        private readonly IStorageFolder _root;
        private readonly Action<string> _log;
        private readonly bool _privateWorkspace;
        private readonly bool _overwrite;
        private readonly Dictionary<Stream, IStorageFile> _files = new();
        private readonly Dictionary<string, IStorageFolder> _folders = new(StringComparer.Ordinal);

        public StorageFolderExtractionDestination(IStorageFolder root, Action<string> log,
            bool privateWorkspace = false, bool overwrite = false)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _log = log ?? (_ => { });
            _privateWorkspace = privateWorkspace;
            _overwrite = overwrite;
        }

        public async Task<Stream?> OpenExistingFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segments = SafeExtractionPath.GetSegments(relativePath);
            var folder = _root;
            var prefix = "";
            foreach (var segment in segments[..^1])
            {
                cancellationToken.ThrowIfCancellationRequested();
                prefix = prefix.Length == 0 ? segment : prefix + "/" + segment;
                if (_folders.TryGetValue(prefix, out var cached))
                {
                    folder = cached;
                    continue;
                }
                var child = await folder.GetFolderAsync(segment);
                if (child is null) return null;
                if (!string.Equals(child.Name, segment, StringComparison.Ordinal))
                {
                    child.Dispose();
                    throw new PkgExtractionException($"O provedor alterou o nome da pasta '{segment}'.");
                }
                _folders[prefix] = folder = child;
            }
            cancellationToken.ThrowIfCancellationRequested();
            using var file = await folder.GetFileAsync(segments[^1]);
            if (file is null) return null;
            if (!string.Equals(file.Name, segments[^1], StringComparison.Ordinal))
                throw new PkgExtractionException($"O provedor alterou o nome de '{relativePath}'.");
            return await file.OpenReadAsync();
        }

        public async Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segments = SafeExtractionPath.GetSegments(relativePath);
            var folder = _root;

            var folderPath = "";
            foreach (var segment in segments[..^1])
            {
                cancellationToken.ThrowIfCancellationRequested();
                folderPath = folderPath.Length == 0 ? segment : folderPath + "/" + segment;
                if (_folders.TryGetValue(folderPath, out var cached))
                {
                    folder = cached;
                    continue;
                }
                var child = await folder.GetFolderAsync(segment);

                if (child is null)
                {
                    _log($"Criando diretório de saída: {segment}");
                    if (!_privateWorkspace && await folder.GetFileAsync(segment) is not null)
                        throw new PkgExtractionException($"Um arquivo ocupa o caminho da pasta '{segment}'.");
                    child = await folder.CreateFolderAsync(segment);
                    if (child is not null && !string.Equals(child.Name, segment, StringComparison.Ordinal))
                    {
                        try { await child.DeleteAsync(); } catch { }
                        throw new PkgExtractionException($"O provedor alterou o nome da pasta '{segment}'.");
                    }
                }

                folder = child ?? throw new PkgExtractionException($"O provedor de armazenamento não conseguiu abrir nem criar o diretório '{segment}'.");
                _folders[folderPath] = folder;
            }

            // Grava diretamente no arquivo final do SAF: evita copiar cada arquivo extraído
            // novamente do temporário para o destino, o que era muito lento em PKGs grandes.
            var name = segments[^1];
            if (!_privateWorkspace && (await folder.GetFileAsync(name) is not null || await folder.GetFolderAsync(name) is not null))
                throw new PkgExtractionException($"O arquivo já existe: {relativePath}");
            cancellationToken.ThrowIfCancellationRequested();
            var existing = _overwrite ? await folder.GetFileAsync(name) : null;
            var file = existing ?? await folder.CreateFileAsync(name);
            if (file is null)
                throw new PkgExtractionException($"O provedor de armazenamento não criou o arquivo de destino para '{relativePath}'.");
            try
            {
                if (!string.Equals(file.Name, name, StringComparison.Ordinal))
                    throw new PkgExtractionException($"O provedor alterou o nome de '{relativePath}'.");
                cancellationToken.ThrowIfCancellationRequested();
                var stream = await file.OpenWriteAsync();
                _files.Add(stream, file);
                return stream;
            }
            catch
            {
                try { await file.DeleteAsync(); } catch { }
                throw;
            }
        }

        public async Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_files.ContainsKey(temporaryFile))
                throw new PkgExtractionException("Arquivo temporário de destino inválido.");
            await temporaryFile.DisposeAsync();
            _files.Remove(temporaryFile);
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

}
