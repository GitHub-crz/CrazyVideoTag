using System.IO;

namespace CrazyVideoTag.Services;

public sealed class FileDeleteService
{
    public void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
