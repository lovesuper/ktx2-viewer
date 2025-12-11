using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KtxViewer.UI.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isInverse = parameter?.ToString() == "Inverse";
        var isNull = value == null;

        if (targetType == typeof(bool))
        {
            return isInverse ? !isNull : isNull;
        }

        return (isInverse ? !isNull : isNull) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
