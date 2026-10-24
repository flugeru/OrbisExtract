using Avalonia.Controls;
using OrbisExtract.App.Views;

namespace OrbisExtract.App;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "OrbisExtract";
        Width = 1120;
        Height = 780;
        MinWidth = 360;
        MinHeight = 560;
        Background = UiTheme.Brush(UiTheme.Background);

        // A navegação (lateral no desktop, inferior em telas estreitas/Android)
        // vive dentro do MainView, para que todas as plataformas compartilhem o mesmo shell.
        Content = new MainView();
    }
}
