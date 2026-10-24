using Avalonia.Controls;
using Avalonia.Media;
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
        Background = new SolidColorBrush(Color.Parse("#0B0F14"));
        Content = new MainView();
    }
}