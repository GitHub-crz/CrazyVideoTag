using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CrazyVideoTag.Converters;

public sealed class ColorStringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value?.ToString() ?? "#4F8EF7"));
        }
        catch
        {
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 142, 247));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;
}
