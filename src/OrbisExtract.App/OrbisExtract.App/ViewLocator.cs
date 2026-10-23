using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace OrbisExtract.App;

public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data) => null;
    public bool Match(object? data) => false;
}