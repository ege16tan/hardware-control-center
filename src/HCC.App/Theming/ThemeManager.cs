using System.Windows;
using HCC.Infrastructure.Configuration;

namespace HCC.App.Theming;

/// <summary>
/// Swaps the theme resource dictionary at runtime. Views must reference theme
/// brushes with DynamicResource so the swap reaches controls that are already loaded.
/// </summary>
public static class ThemeManager
{
    private const string ThemeFileMarker = "Theme.xaml";

    public static void Apply(string? theme)
    {
        var application = Application.Current;
        if (application is null) return;

        var fileName = string.Equals(theme, ThemeSettings.Light, StringComparison.OrdinalIgnoreCase)
            ? "Themes/LightTheme.xaml"
            : "Themes/DarkTheme.xaml";
        var next = new ResourceDictionary { Source = new Uri(fileName, UriKind.Relative) };

        var merged = application.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains(ThemeFileMarker, StringComparison.OrdinalIgnoreCase) == true);

        if (existing is null) merged.Insert(0, next);
        else merged[merged.IndexOf(existing)] = next;
    }
}
