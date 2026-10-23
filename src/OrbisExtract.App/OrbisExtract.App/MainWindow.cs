using Avalonia.Controls;
using OrbisExtract.App.Views;

namespace OrbisExtract.App;

public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "OrbisExtract";
        Width = 900;
        Height = 650;
        MinWidth = 360;
        MinHeight = 520;
        Content = new MainView();
    }
}