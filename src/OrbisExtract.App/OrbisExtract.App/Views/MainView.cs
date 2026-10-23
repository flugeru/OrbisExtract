using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia;

namespace OrbisExtract.App.Views;

public sealed class MainView : UserControl
{
    private readonly TextBlock _selectedFile;
    private readonly TextBlock _status;

    public MainView()
    {
        _selectedFile = new TextBlock
        {
            Text = "Nenhum arquivo PKG selecionado",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Gray
        };
        _status = new TextBlock
        {
            Text = "Selecione um arquivo .pkg para começar.",
            TextWrapping = TextWrapping.Wrap
        };

        var chooseFile = new Avalonia.Controls.Button
        {
            Content = "Selecionar arquivo PKG",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 12)
        };
        chooseFile.Click += async (_, _) => await ChoosePackageAsync();

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 18,
                Margin = new Thickness(24),
                MaxWidth = 720,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "OrbisExtract",
                        FontSize = 30,
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Extrator de arquivos PlayStation 4 PKG",
                        FontSize = 16,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new Border
                    {
                        Padding = new Thickness(18),
                        CornerRadius = new CornerRadius(12),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.Parse("#667080")),
                        Child = new StackPanel
                        {
                            Spacing = 12,
                            Children = { _selectedFile, chooseFile, _status }
                        }
                    },
                    new TextBlock
                    {
                        Text = "A abertura e a extração dependem de PKGs compatíveis com a biblioteca LibOrbisPkg e das chaves/passcodes legítimos necessários.",
                        TextWrapping = TextWrapping.Wrap,
                        Opacity = 0.75
                    }
                }
            }
        };
    }

    private async Task ChoosePackageAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            _status.Text = "O seletor de arquivos não está disponível nesta plataforma.";
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar pacote PKG",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Pacote PlayStation PKG")
                {
                    Patterns = ["*.pkg", "*.PKG"]
                },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0)
            return;

        _selectedFile.Text = files[0].Name;
        _status.Text = "Arquivo selecionado. A interface de extração completa ainda precisa ser reconectada ao fluxo de extração.";
    }
}