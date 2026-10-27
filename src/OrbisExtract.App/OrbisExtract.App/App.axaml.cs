using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Markup.Xaml;
using OrbisExtract.App.Views;
using OrbisExtract.App.Localization;

namespace OrbisExtract.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var mainView = new MainView();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Window
                {
                    Title = "OrbisExtract",
                    Width = 1000,
                    Height = 700,
                    MinWidth = 360,
                    MinHeight = 520,
                    Content = mainView
                };
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
