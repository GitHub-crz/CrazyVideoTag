using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CrazyVideoTag.Converters;

public sealed class ColorStringToBrushConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, SolidColorBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var colorText = value?.ToString();
        if (string.IsNullOrWhiteSpace(colorText))
        {
            colorText = "#4F8EF7";
        }

        return BrushCache.GetOrAdd(colorText, CreateBrush);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => System.Windows.Data.Binding.DoNothing;

    private static SolidColorBrush CreateBrush(string colorText)
    {
        try
        {
            var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorText));
            brush.Freeze();
            return brush;
        }
        catch
        {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 142, 247));
            brush.Freeze();
            return brush;
        }
    }
}
