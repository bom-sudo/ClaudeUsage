using ClaudeUsage.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace ClaudeUsage.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Collapsed;
}

/// <summary>Standard WinUI recipe for binding a RadioButton group to an enum: ConverterParameter names the enum value this button represents.</summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var parameterString = parameter as string;
        if (parameterString is null || value is null)
        {
            return false;
        }

        return value.ToString() == parameterString;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        var parameterString = parameter as string;
        if (parameterString is null || value is not true)
        {
            // Leaves the source untouched — fires when the previously-checked RadioButton in the
            // group unchecks itself, which must not overwrite the value the newly-checked one just set.
            return DependencyProperty.UnsetValue;
        }

        return Enum.Parse(targetType, parameterString);
    }
}

/// <summary>
/// Maps usage/connection states to Fluent's own semantic system brushes (looked up by their well-known
/// resource key, e.g. "SystemFillColorCriticalBrush") rather than custom colors, so the palette stays
/// consistent with the rest of Windows 11. Resource dictionary lookups resolve theme-dictionary entries
/// against the active theme on every call, so this stays correct across light/dark/high-contrast.
/// </summary>
public sealed class UsageStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value is UsageState state
            ? state switch
            {
                UsageState.Normal => "AccentFillColorDefaultBrush",
                UsageState.Moderate => "SystemFillColorCautionBrush",
                UsageState.Warning => "SystemFillColorCautionBrush",
                UsageState.Critical => "SystemFillColorCriticalBrush",
                _ => "AccentFillColorDefaultBrush",
            }
            : "AccentFillColorDefaultBrush";

        return (Brush)Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value is ApiConnectionState state
            ? state switch
            {
                ApiConnectionState.Connected => "SystemFillColorSuccessBrush",
                ApiConnectionState.Connecting => "SystemFillColorCautionBrush",
                ApiConnectionState.Unauthorized or ApiConnectionState.RateLimited or ApiConnectionState.Error => "SystemFillColorCriticalBrush",
                ApiConnectionState.Offline => "TextFillColorDisabledBrush",
                _ => "TextFillColorDisabledBrush",
            }
            : "TextFillColorDisabledBrush";

        return (Brush)Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public sealed class BoolNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is not true;
}

public sealed class DoubleToPercentStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is double d ? $"{Math.Round(d * 100)}%" : "0%";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is string s && double.TryParse(s.TrimEnd('%'), out var d) ? d / 100.0 : 0.0;
}
