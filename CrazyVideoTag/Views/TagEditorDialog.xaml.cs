using System.Windows;

namespace CrazyVideoTag.Views;

public partial class TagEditorDialog : Window
{
    public static readonly string[] Colors =
    [
        "#4F8EF7", "#2563EB", "#1D4ED8", "#06B6D4", "#0891B2", "#14B8A6", "#0D9488", "#22C55E",
        "#16A34A", "#84CC16", "#65A30D", "#F59E0B", "#D97706", "#F97316", "#EA580C", "#EF4444",
        "#DC2626", "#F43F5E", "#E11D48", "#EC4899", "#DB2777", "#A855F7", "#9333EA", "#7C3AED",
        "#6366F1", "#475569", "#64748B", "#94A3B8"
    ];

    public TagEditorDialog(Models.TagKind kind)
    {
        InitializeComponent();
        Title = kind == Models.TagKind.Actor ? "添加演员" : "添加标签";
        ColorList.ItemsSource = Colors;
        ColorList.SelectedIndex = 0;
        NameBox.Focus();
    }

    public TagEditorDialog(Models.TagKind kind, string name, string color) : this(kind)
    {
        Title = kind == Models.TagKind.Actor ? "编辑演员" : "编辑标签";
        NameBox.Text = name;
        ColorList.SelectedItem = Colors.Contains(color) ? color : Colors[0];
        NameBox.SelectAll();
    }

    public string TagName => NameBox.Text.Trim();
    public string SelectedColor => ColorList.SelectedItem?.ToString() ?? Colors[0];

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TagName))
        {
            System.Windows.MessageBox.Show("请输入名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
