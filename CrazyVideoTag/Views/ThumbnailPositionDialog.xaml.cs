using System.Globalization;
using System.Windows;

namespace CrazyVideoTag.Views;

public partial class ThumbnailPositionDialog : Window
{
    public ThumbnailPositionDialog()
    {
        InitializeComponent();
        PercentageBox.Focus();
        PercentageBox.SelectAll();
    }

    public double Percentage { get; private set; } = 50;

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string value } || !TryParsePercentage(value, out var percentage))
        {
            return;
        }

        Percentage = percentage;
        DialogResult = true;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParsePercentage(PercentageBox.Text, out var percentage))
        {
            System.Windows.MessageBox.Show("请输入 1 到 99 之间的数字。", "比例无效", MessageBoxButton.OK, MessageBoxImage.Information);
            PercentageBox.Focus();
            PercentageBox.SelectAll();
            return;
        }

        Percentage = percentage;
        DialogResult = true;
    }

    private static bool TryParsePercentage(string value, out double percentage)
    {
        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out percentage)
            && percentage is >= 1 and <= 99;
    }
}
