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
            // A fully decorated window draws a second Windows caption over the
            // Avalonia title bar. Keep only the border, not native caption buttons.
            WindowDecorations = WindowDecorations.BorderOnly
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
        // With WindowDecorations.None, Windows no longer supplies resize hit targets.
        // Add transparent 6px edge / 12px corner hit zones, preserving the clean theme.
        var resizeLayer = new Grid
        {
            RowDefinitions = new RowDefinitions("12,*,12"),
            ColumnDefinitions = new ColumnDefinitions("12,*,12"),
            IsHitTestVisible = true
        };
        void AddResizeHandle(int row, int column, WindowEdge edge, StandardCursorType cursorType,
            bool horizontal)
        {
            var handle = new Border
            {
                Background = Brushes.Transparent,
                Cursor = new Cursor(cursorType),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = horizontal ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 6, 0)
            };
            handle.PointerPressed += (_, e) =>
            {
                if (window.WindowState != WindowState.Normal ||
                    !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
                    return;
                window.BeginResizeDrag(edge, e);
                e.Handled = true;
            };
            Grid.SetRow(handle, row);
            Grid.SetColumn(handle, column);
            resizeLayer.Children.Add(handle);
        }
        AddResizeHandle(0, 0, WindowEdge.NorthWest, StandardCursorType.TopLeftCorner, true);
        AddResizeHandle(0, 1, WindowEdge.North, StandardCursorType.TopSide, true);
        AddResizeHandle(0, 2, WindowEdge.NorthEast, StandardCursorType.TopRightCorner, true);
        AddResizeHandle(1, 0, WindowEdge.West, StandardCursorType.LeftSide, false);
        AddResizeHandle(1, 2, WindowEdge.East, StandardCursorType.RightSide, false);
        AddResizeHandle(2, 0, WindowEdge.SouthWest, StandardCursorType.BottomLeftCorner, true);
        AddResizeHandle(2, 1, WindowEdge.South, StandardCursorType.BottomSide, true);
        AddResizeHandle(2, 2, WindowEdge.SouthEast, StandardCursorType.BottomRightCorner, true);
        resizeLayer.IsVisible = window.WindowState == WindowState.Normal;
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Window.WindowStateProperty)
                resizeLayer.IsVisible = window.WindowState == WindowState.Normal;
        };
        var root = new Grid();
        root.Children.Add(layout);
        root.Children.Add(resizeLayer);
        window.Content = root;
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
