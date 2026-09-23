using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NPTELManagement.Desktop.Common;

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return false;
    }
}

public class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return Visibility.Collapsed;
        if (value is string s && string.IsNullOrWhiteSpace(s)) return Visibility.Collapsed;
        if (value is System.Collections.ICollection c && c.Count == 0) return Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseNullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return Visibility.Visible;
        if (value is string s && string.IsNullOrWhiteSpace(s)) return Visibility.Visible;
        if (value is System.Collections.ICollection c && c.Count == 0) return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TimelineStatusToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.Trim();
        return status switch
        {
            "Completed" => "✓",
            "Current" => "●",
            "Overdue" => "!",
            _ => "○"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class TimelineStatusToBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush CompletedBrush = new(System.Windows.Media.Color.FromRgb(16, 185, 129));
    private static readonly System.Windows.Media.SolidColorBrush CurrentBrush = new(System.Windows.Media.Color.FromRgb(37, 99, 235));
    private static readonly System.Windows.Media.SolidColorBrush OverdueBrush = new(System.Windows.Media.Color.FromRgb(239, 68, 68));
    private static readonly System.Windows.Media.SolidColorBrush PendingBrush = new(System.Windows.Media.Color.FromRgb(148, 163, 184));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.Trim();
        return status switch
        {
            "Completed" => CompletedBrush,
            "Current" => CurrentBrush,
            "Overdue" => OverdueBrush,
            _ => PendingBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
