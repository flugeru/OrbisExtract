using System;
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
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OrbisExtract.Core;
using OrbisExtract.Infrastructure.OpenOrbis;
using OrbisExtract.App.Updates;
using OrbisExtract.App.Localization;
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
    private int _contentColumns;
    private readonly Border _aboutOverlay;
    private readonly Border _languageOverlay;
    private readonly Border _updateOverlay;
    private readonly TextBlock _updateMessage;
    private readonly Button _updateOpen;
    private Button _updateDismiss = null!;
    private TextBlock _aboutChangelog = null!;
    private TextBlock _repositoryError = null!;
    private string? _updateUrl;
    private bool _startupUpdateChecked;

    // Estado
    private IStorageFile? _package;
    private IStorageFolder? _destination;
    private CancellationTokenSource? _run;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Queue<Control> _logLines = new();
    private readonly List<string> _logMessages = new();
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
        _infoRows = new StackPanel
        {
            Spacing = 10,
            IsVisible = false,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { Field("Jogo", _gameTitle), Field("Versão", _version), Field("Content ID", _contentId) }
        };
        _choosePackage = UiTheme.ActionButton("Selecionar arquivo PKG");
        _choosePackage.Click += async (_, _) => await ChoosePackageAsync();

        _packageCard = Section("Arquivo do pacote",
            "Selecione o PKG para consultar os metadados antes de extrair.",
            _fileName, _chips, _infoRows, _choosePackage);

        // ---------- Cartão de configuração (passcode removido da UI) ----------
        _destinationName = UiTheme.Text("Nenhuma pasta escolhida", 14, UiTheme.TextSecondary);
        _chooseDestination = UiTheme.ActionButton("Escolher pasta");
        _chooseDestination.Click += async (_, _) => await ChooseDestinationAsync();
        _extract = UiTheme.ActionButton("Extrair arquivos", primary: true);
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
        _cancel.HorizontalAlignment = HorizontalAlignment.Right;
        _cancel.VerticalAlignment = VerticalAlignment.Center;
        _cancel.Margin = new Thickness(8, 0, 0, 0);
        _cancel.Foreground = UiTheme.Brush(UiTheme.Primary);
        _cancel.Click += (_, _) => _run?.Cancel();

        _logPanel = new StackPanel { Spacing = 0 };
        _logScroll = new ScrollViewer
        {
            MaxHeight = 260,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
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

        var metaRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
        metaRow.Children.Add(_counter);
        Grid.SetColumn(_elapsed, 1);
        metaRow.Children.Add(_elapsed);
        Grid.SetColumn(_cancel, 2);
        metaRow.Children.Add(_cancel);

        _monitorCard = UiTheme.Card(new StackPanel
        {
            Spacing = 10,
            Children = { header, _progress, metaRow, _status, _banner, logBox }
        });

        // ---------- Grid responsivo do conteúdo ----------
        _contentGrid = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _contentGrid.Children.Add(_packageCard);
        _contentGrid.Children.Add(_settingsCard);
        _contentGrid.Children.Add(_monitorCard);

        var mainGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*") };
        _aboutOverlay = CreateAboutOverlay();
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
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            Content = centered
        };
        scroll.SizeChanged += (_, e) =>
        {
            ApplyContentLayout(e.NewSize.Width);
            // On narrow phones keep the subtitle full-width below the action icons.
            var narrow = e.NewSize.Width < 390;
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
        _languageOverlay = CreateLanguageOverlay();
        mainGrid.Children.Add(_languageOverlay);
        mainGrid.Children.Add(_updateOverlay);
        wrapper.Children.Add(mainGrid);
        Content = wrapper;

        foreach (var label in this.GetLogicalDescendants().OfType<TextBlock>())
            if (label.Text is { } text) _localizedLabels[label] = text;
        foreach (var label in new[] { _fileName, _destinationName, _gameTitle, _version, _contentId, _stateTitle, _counter, _status, _bannerText, _updateMessage, _aboutChangelog })
            _localizedLabels.Remove(label);
        foreach (var button in this.GetLogicalDescendants().OfType<Button>())
            if (button.Content is string text) _localizedButtons[button] = text;
        AppLanguage.Changed += ApplyLanguage;
        DetachedFromVisualTree += (_, _) => AppLanguage.Changed -= ApplyLanguage;
        AttachedToVisualTree += (_, _) => { AppLanguage.Changed -= ApplyLanguage; AppLanguage.Changed += ApplyLanguage; };
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
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { settings, about } };
        return actions;
    }

    private static Button CreateRoundHeaderButton(bool settings, string tip)
    {
        // Both actions use the same outlined icon stroke, rather than mixing font glyphs.
        var icon = new Grid { Width = 24, Height = 24 };
        void Line(string data) => icon.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data), Stroke = UiTheme.Brush(UiTheme.Primary), StrokeThickness = 1.8,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round
        });
        if (settings)
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
        foreach (var (box, original) in _logEntries) box.Text = AppLanguage.Message(original);
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
        const string license = "MIT License\n\nCopyright (c) 2026 flugeru\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the Software), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\nThe above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\nTHE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.";
        var close = UiTheme.ActionButton("Fechar");
        var licenseBox = new Border
        {
            Background = UiTheme.Brush(UiTheme.SurfaceElevated),
            BorderBrush = UiTheme.Brush(UiTheme.Outline),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(UiTheme.CardRadius),
            Padding = new Thickness(14),
            Child = new ScrollViewer
            {
                MaxHeight = 120,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Content = UiTheme.Text(license, 12, UiTheme.TextSecondary)
            }
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
                AboutSection("Licença MIT", licenseBox)
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
            var logText = new TextBox
            {
                Text = AppLanguage.Message(message),
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
        if (top is null) return;

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = AppLanguage.Text("Selecionar pacote PlayStation PKG"),
                AllowMultiple = false,
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

        _package = files[0];
        SetLiteral(_fileName, _package.Name);
        _fileName.Foreground = UiTheme.Brush(UiTheme.TextPrimary);
        _infoRows.IsVisible = false;
        _chips.Children.Clear();
        _chips.Children.Add(UiTheme.Chip("PS4"));
        _chips.IsVisible = true;
        SetLocalized(_counter, () => AppLanguage.Text("Arquivo selecionado."));
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
                SetLocalized(_counter, () => AppLanguage.Text("Metadados serão validados durante a extração."));
            }
            else
            {
                Log("Lendo metadados do PKG…");
                var info = await new OpenOrbisPkgMetadataReader().ReadAsync(stream, _lifetime.Token);
                Log("Metadados do PKG lidos.");
                SetLiteral(_gameTitle, info.Title?.ToString() ?? "—");
                SetLiteral(_version, info.Version?.ToString() ?? "—");
                SetLiteral(_contentId, info.ContentId?.ToString() ?? "—");
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
        SetLocalized(_stateTitle, () => AppLanguage.Text("Preparando"));
        SetLocalized(_counter, () => AppLanguage.Text("Preparando a extração…"));
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
            // Ativa o serviço foreground antes de abrir/copiar um PKG grande.
            // Assim o Android não pode encerrar o processo durante a preparação inicial.
            OrbisExtract.App.Shared.AppNotificationHelper.StartExtraction(
                _package.Name, _destination.Name, 0, 0);

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
                SetLocalized(_stateTitle, () => AppLanguage.Text("Preparando pacote"));
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
                    SetLocalized(_stateTitle, () => AppLanguage.Text(p.State == "Concluído" ? "Concluindo" : p.State));
                    SetLocalized(_counter, () => total > 0
                        ? $"{p.FilesCompleted:N0} {AppLanguage.Text("de")} {total:N0} {AppLanguage.Text("arquivos")}"
                        : $"{p.FilesCompleted:N0} {AppLanguage.Text("arquivos")}");
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
            SetLocalized(_stateTitle, () => AppLanguage.Text("Concluído"));
            SetLocalized(_counter, () => $"{result.FilesExtracted:N0} {AppLanguage.Text("arquivos")} · {result.BytesExtracted / 1024d / 1024d:N1} MiB");
            
            // Mostra notificação Android de sucesso se estiver no Android
            if (OperatingSystem.IsAndroid())
            {
                var top = TopLevel.GetTopLevel(this);
                if (top != null)
                {
                    OrbisExtract.App.Shared.AppNotificationHelper.CompleteExtraction(result.FilesExtracted, result.BytesExtracted);
                }
            }
            
            SetBanner(() => $"{AppLanguage.Text("Extração concluída com sucesso em")} {_clock.Elapsed:mm\\:ss}.", UiTheme.Success);
            Log($"Extração concluída. Arquivos: {result.FilesExtracted:N0}; bytes: {result.BytesExtracted:N0}.");
        }
        catch (OperationCanceledException)
        {
            _progress.IsIndeterminate = false;
            SetLocalized(_stateTitle, () => AppLanguage.Text("Cancelado"));
            SetLocalized(_counter, () => AppLanguage.Text("A extração foi interrompida."));
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
            SetLocalized(_stateTitle, () => AppLanguage.Text("Falha na extração"));
            SetLocalized(_counter, () => AppLanguage.Text("Veja o log detalhado para mais informações."));
            SetBanner(() => $"{AppLanguage.Text("Falha na extração:")} {ex.Message}", UiTheme.Error);
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
