using System.Globalization;
using System.Windows.Data;

namespace CrazyVideoTag.Converters;

public sealed class TagIdsToDefinitionsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not IEnumerable<string> ids || values[1] is not IEnumerable<Models.TagDefinition> tags)
        {
            return Array.Empty<Models.TagDefinition>();
        }

        var idSet = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return tags.Where(tag => idSet.Contains(tag.Id)).ToList();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => targetTypes.Select(_ => System.Windows.Data.Binding.DoNothing).ToArray();
}
