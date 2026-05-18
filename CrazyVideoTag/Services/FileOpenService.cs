using System.Diagnostics;
using System.IO;

namespace CrazyVideoTag.Services;

public sealed class FileOpenService
{
    public void Open(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("视频文件不存在。", path);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
