using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CrazyVideoTag.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase);
        var visible = value switch
        {
            null => false,
            bool boolean => boolean,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true
        };

        if (invert)
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}
