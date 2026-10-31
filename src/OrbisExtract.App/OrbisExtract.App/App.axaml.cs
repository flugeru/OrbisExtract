using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using OrbisExtract.App.Views;
using OrbisExtract.App.Localization;

namespace OrbisExtract.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    // Desktop-only custom title bar without the native gray Windows border.
    private static Window CreateDesktopWindow(Control content)
    {
        var window = new Window
        {
            Title = "OrbisExtract",
            Width = 1000,
            Height = 700,
            MinWidth = 360,
            MinHeight = 520,
            Background = UiTheme.Brush(UiTheme.Background),
            WindowDecorations = WindowDecorations.None
        };

        var minimize = TitleButton("─", "Minimizar");
        var maximize = TitleButton("□", "Maximizar ou restaurar");
        var close = TitleButton("×", "Fechar", true);
        minimize.Click += (_, _) => window.WindowState = WindowState.Minimized;
        maximize.Click += (_, _) => window.WindowState =
            window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        close.Click += (_, _) => window.Close();

        void UpdateMaximizeGlyph() =>
            maximize.Content = window.WindowState == WindowState.Maximized ? "❐" : "□";
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
                UpdateMaximizeGlyph();
        };

        var title = new TextBlock
        {
            Text = "OrbisExtract",
            FontSize = 13,
            Foreground = UiTheme.Brush(UiTheme.TextSecondary),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0)
        };
        var dragArea = new Border
        {
            Background = Brushes.Transparent,
            Child = title
        };
        dragArea.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(dragArea).Properties.IsLeftButtonPressed)
                return;
            if (e.ClickCount == 2)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                window.BeginMoveDrag(e);
            }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { minimize, maximize, close }
        };
        var titleBar = new Grid
        {
            Height = 40,
            Background = UiTheme.Brush(UiTheme.Background),
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        titleBar.Children.Add(dragArea);
        Grid.SetColumn(buttons, 1);
        titleBar.Children.Add(buttons);

        var layout = new Grid { RowDefinitions = new RowDefinitions("40,*"), Background = UiTheme.Brush(UiTheme.Background) };
        layout.Children.Add(titleBar);
        Grid.SetRow(content, 1);
        layout.Children.Add(content);
        window.Content = layout;
        return window;
    }

    private static Button TitleButton(string glyph, string tooltip, bool isClose = false)
    {
        var button = new Button
        {
            Content = glyph,
            Width = 46,
            Height = 40,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = UiTheme.Brush(UiTheme.TextSecondary),
            FontSize = 17,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(button, tooltip);
        button.PointerEntered += (_, _) =>
        {
            button.Background = UiTheme.Brush(isClose ? "#B32636" : UiTheme.SurfaceElevated);
            button.Foreground = UiTheme.Brush(UiTheme.TextPrimary);
        };
        button.PointerExited += (_, _) =>
        {
            button.Background = Brushes.Transparent;
            button.Foreground = UiTheme.Brush(UiTheme.TextSecondary);
        };
        return button;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var mainView = new MainView();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = CreateDesktopWindow(mainView);
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = mainView;
            }
        }
        catch (Exception ex)
        {
            // Never leave a blank screen if the UI fails during startup.
            Debug.WriteLine($"[OrbisExtract startup] {ex}");
            var errorView = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Spacing = 12,
                    Margin = new Thickness(20),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = AppLanguage.Text("OrbisExtract não conseguiu iniciar a interface"),
                            FontSize = 22,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = Brushes.White,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = ex.ToString(),
                            FontSize = 13,
                            Foreground = Brushes.OrangeRed,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                },
                Background = new SolidColorBrush(Color.Parse("#0B0F14"))
            };

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Window
                {
                    Title = AppLanguage.Text("OrbisExtract — erro de inicialização"),
                    Width = 800,
                    Height = 600,
                    Content = errorView
                };
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = errorView;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
