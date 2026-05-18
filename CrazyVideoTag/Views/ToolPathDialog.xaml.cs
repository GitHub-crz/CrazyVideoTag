using System.Windows;

namespace CrazyVideoTag.Views;

public partial class ToolPathDialog : Window
{
    public ToolPathDialog(string ffmpegPath, string ffprobePath)
    {
        InitializeComponent();
        FfmpegBox.Text = ffmpegPath;
        FfprobeBox.Text = ffprobePath;
    }

    public string FfmpegPath => FfmpegBox.Text.Trim();
    public string FfprobePath => FfprobeBox.Text.Trim();

    private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e) => Browse(FfmpegBox);

    private void BrowseFfprobe_Click(object sender, RoutedEventArgs e) => Browse(FfprobeBox);

    private static void Browse(System.Windows.Controls.TextBox target)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Filter = "Executable|*.exe|All files|*.*",
            FileName = target.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FfmpegPath) || string.IsNullOrWhiteSpace(FfprobePath))
        {
            System.Windows.MessageBox.Show("请填写 ffmpeg.exe 和 ffprobe.exe 的路径。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
