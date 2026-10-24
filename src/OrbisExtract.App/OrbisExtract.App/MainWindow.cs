using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Button = Avalonia.Controls.Button;
using OrbisExtract.App.Views;

namespace OrbisExtract.App;

public sealed class MainWindow : Window
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#0B0F14"));
    private static readonly IBrush SurfaceBrush = new SolidColorBrush(Color.Parse("#151B24"));
    private static readonly IBrush PrimaryBrush = new SolidColorBrush(Color.Parse("#B9A7FF"));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#F5F7FA"));
    private static readonly IBrush SecondaryBrush = new SolidColorBrush(Color.Parse("#AAB4C3"));
    private static readonly IBrush UiBorderBrush = new SolidColorBrush(Color.Parse("#2B3542"));

    public MainWindow()
    {
        Title = "OrbisExtract";
        Width = 1120;
        Height = 780;
        MinWidth = 360;
        MinHeight = 560;
        Background = BackgroundBrush;

        var content = new MainView();
        var shell = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("224,*"),
            Background = BackgroundBrush
        };

        var sidebar = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = UiBorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(18, 26),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "ORBIS",
                                FontSize = 12,
                                FontWeight = FontWeight.Bold,
                                Foreground = PrimaryBrush,
                                Margin = new Thickness(12, 0, 0, 0)
                            },
                            new TextBlock
                            {
                                Text = "OrbisExtract",
                                FontSize = 20,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = TextBrush,
                                Margin = new Thickness(12, 0, 0, 24)
                            },
                            NavItem("◈", "Extrair", selected: true),
                            NavItem("◷", "Histórico", enabled: false),
                            NavItem("⚙", "Configurações", enabled: false)
                        }
                    },
                    new StackPanel
                    {
                        Spacing = 6,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Children =
                        {
                            new Border { Height = 1, Background = UiBorderBrush, Margin = new Thickness(0, 0, 0, 12) },
                            new TextBlock
                            {
                                Text = "●  Aplicativo pronto",
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.Parse("#71D6A3")),
                                Margin = new Thickness(12, 0)
                            },
                            new TextBlock
                            {
                                Text = "PKG · PlayStation 4",
                                FontSize = 11,
                                Foreground = SecondaryBrush,
                                Margin = new Thickness(12, 0, 0, 4)
                            }
                        }
                    }
                }
            }
        };
        DockPanel.SetDock(sidebar.Child is DockPanel dp ? dp.Children[1] : null!, Dock.Bottom);
        Grid.SetColumn(sidebar, 0);
        Grid.SetColumn(content, 1);
        shell.Children.Add(sidebar);
        shell.Children.Add(content);
        Content = shell;
    }

    private static Button NavItem(string glyph, string label, bool selected = false, bool enabled = true)
    {
        return new Button
        {
            IsEnabled = enabled,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = selected ? new SolidColorBrush(Color.Parse("#29243B")) : Brushes.Transparent,
            Foreground = selected ? PrimaryBrush : enabled ? SecondaryBrush : new SolidColorBrush(Color.Parse("#667181")),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 13),
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
    }
}
