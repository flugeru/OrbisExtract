using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using System.Collections.Generic;
using Button = Avalonia.Controls.Button;

namespace OrbisExtract.App.Views;

/// <summary>Tokens (cores, tipografia) e fábricas de controles do tema escuro Material 3.</summary>
public static class UiTheme
{
    public const string Background = "#0B0F14";
    public const string Surface = "#151B24";
    public const string SurfaceElevated = "#1D2530";
    public const string Outline = "#2B3542";
    public const string Primary = "#B9A7FF";
    public const string PrimaryContainer = "#29243B";
    public const string OnPrimary = "#1A1033";
    public const string Progress = Primary;
    public const string Success = "#71D6A3";
    public const string Warning = "#FFCC80";
    public const string Error = "#FF8A8A";
    public const string TextPrimary = "#F5F7FA";
    public const string TextSecondary = "#AAB4C3";
    public const string TextDisabled = "#667181";

    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace");

    private static readonly Dictionary<string, IBrush> Cache = new();

    public static IBrush Brush(string hex)
    {
        if (!Cache.TryGetValue(hex, out var brush))
            Cache[hex] = brush = new SolidColorBrush(Color.Parse(hex)).ToImmutable();
        return brush;
    }

    public static TextBlock Text(string text, double size = 15, string color = TextPrimary,
        FontWeight weight = FontWeight.Normal) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = Brush(color),
        TextWrapping = TextWrapping.Wrap
    };

    public static Border Card(Control child, double padding = 20) => new()
    {
        Background = Brush(Surface),
        BorderBrush = Brush(Outline),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(18),
        Padding = new Thickness(padding),
        Child = child
    };

    public static Border Chip(string text) => new()
    {
        Background = Brush(SurfaceElevated),
        BorderBrush = Brush(Outline),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10, 4),
        Margin = new Thickness(0, 0, 8, 8),
        Child = Text(text, 12, TextSecondary, FontWeight.Medium)
    };

    public static Button PrimaryButton(string text) => Styled(new Button
    {
        Content = text,
        Background = Brush(Primary),
        Foreground = Brush(OnPrimary),
        BorderThickness = new Thickness(0),
        FontWeight = FontWeight.SemiBold
    });

    public static Button OutlinedButton(string text) => Styled(new Button
    {
        Content = text,
        Background = Brushes.Transparent,
        Foreground = Brush(Primary),
        BorderBrush = Brush(Outline),
        BorderThickness = new Thickness(1)
    });

    private static Button Styled(Button button)
    {
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        button.Padding = new Thickness(16, 13);
        button.CornerRadius = new CornerRadius(12);
        button.FontSize = 14;
        button.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputElement.IsEnabledProperty)
                button.Opacity = button.IsEnabled ? 1 : 0.4;
        };
        return button;
    }
}
