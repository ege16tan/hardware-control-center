using System.Windows;

namespace HCC.App.Controls;

/// <summary>
/// Marks the navigation entry of the page that is currently shown. It is an attached
/// property so <c>RailButtonStyle</c> can react to it without knowing the view models.
/// </summary>
public static class NavigationState
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsActive",
            typeof(bool),
            typeof(NavigationState),
            new FrameworkPropertyMetadata(false));

    public static void SetIsActive(DependencyObject element, bool value) => element.SetValue(IsActiveProperty, value);

    public static bool GetIsActive(DependencyObject element) => (bool)element.GetValue(IsActiveProperty);
}
