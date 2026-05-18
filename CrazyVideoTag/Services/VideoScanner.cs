using System.IO;
using CrazyVideoTag.Models;

namespace CrazyVideoTag.Services;

public sealed record ScanProgress(int Count, string? CurrentPath);

public sealed class VideoScanner
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".ts", ".3gp", ".rmvb", ".vob", ".ogv"
    };
    private string _storageFolder = AppContext.BaseDirectory;

    public void SetStorageFolder(string storageFolder)
    {
        _storageFolder = string.IsNullOrWhiteSpace(storageFolder) ? AppContext.BaseDirectory : storageFolder;
    }

    public Task<List<VideoItem>> ScanAsync(string rootFolder, AppState state, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var videos = new List<VideoItem>();
            foreach (var path in EnumerateFilesSafe(rootFolder, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Extensions.Contains(System.IO.Path.GetExtension(path)))
                {
                    continue;
                }

                var info = new FileInfo(path);
                var video = new VideoItem
                {
                    Path = path,
                    ModifiedAt = info.LastWriteTime,
                    Size = info.Length
                };

                if (state.Videos.TryGetValue(path, out var metadata))
                {
                    video.TagIds.AddRange(metadata.TagIds);
                    video.ActorIds.AddRange(metadata.ActorIds);
                    if (!string.IsNullOrWhiteSpace(metadata.CustomCoverPath) && File.Exists(metadata.CustomCoverPath))
                    {
                        video.CustomCoverPath = metadata.CustomCoverPath;
                    }
                }

                if (state.ThumbnailCache.TryGetValue(path, out var cache) && IsCacheValid(info, cache))
                {
                    video.ThumbnailPath = ResolveThumbnailPath(cache.ThumbnailPath);
                    video.ThumbnailError = cache.LastError;
                    if (cache.DurationSeconds is > 0)
                    {
                        video.Duration = TimeSpan.FromSeconds(cache.DurationSeconds.Value);
                    }
                }

                videos.Add(video);
                progress?.Report(new ScanProgress(videos.Count, path));
            }

            return videos.OrderByDescending(video => video.ModifiedAt).ToList();
        }, cancellationToken);
    }

    public static bool IsVideoFile(string path) => Extensions.Contains(System.IO.Path.GetExtension(path));

    public static FolderNode? BuildFolderTree(string rootFolder, IReadOnlyCollection<VideoItem> videos)
    {
        if (videos.Count == 0)
        {
            return null;
        }

        var root = new FolderNode
        {
            Name = new DirectoryInfo(rootFolder).Name,
            Path = rootFolder
        };
        var rootHasVideos = videos.Any(video => string.Equals(video.Folder, rootFolder, StringComparison.OrdinalIgnoreCase));
        if (rootHasVideos)
        {
            root.Children.Add(new FolderNode { Name = root.Name, Path = root.Path });
        }

        foreach (var folder in videos.Select(video => video.Folder).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(folder => folder))
        {
            var relative = System.IO.Path.GetRelativePath(rootFolder, folder);
            var current = root;
            if (relative == ".")
            {
                continue;
            }

            foreach (var part in relative.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
            {
                if (string.IsNullOrWhiteSpace(part) || part == ".")
                {
                    continue;
                }

                var nextPath = System.IO.Path.Combine(current.Path, part);
                var child = current.Children.FirstOrDefault(node => string.Equals(node.Path, nextPath, StringComparison.OrdinalIgnoreCase));
                if (child is null)
                {
                    child = new FolderNode { Name = part, Path = nextPath };
                    current.Children.Add(child);
                }

                current = child;
            }
        }

        return root;
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootFolder, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(rootFolder);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();

            IEnumerable<string> files = [];
            try
            {
                files = Directory.EnumerateFiles(folder);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }

            foreach (var file in files)
            {
                yield return file;
            }

            IEnumerable<string> directories = [];
            try
            {
                directories = Directory.EnumerateDirectories(folder);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }

            foreach (var directory in directories.Where(ShouldScanDirectory))
            {
                pending.Push(directory);
            }
        }
    }

    private static bool ShouldScanDirectory(string directory)
    {
        var name = System.IO.Path.GetFileName(directory);
        if (name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Recycle.Bin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith('$'))
        {
            return false;
        }

        try
        {
            var attributes = File.GetAttributes(directory);
            return !attributes.HasFlag(FileAttributes.Hidden) && !attributes.HasFlag(FileAttributes.System);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool IsCacheValid(FileInfo info, ThumbnailCacheEntry cache)
    {
        var thumbnailPath = ResolveThumbnailPath(cache.ThumbnailPath);
        return cache.FileSize == info.Length
            && cache.LastWriteTicks == info.LastWriteTimeUtc.Ticks
            && (string.IsNullOrWhiteSpace(cache.FileName) || string.Equals(cache.FileName, info.Name, StringComparison.OrdinalIgnoreCase))
            && File.Exists(thumbnailPath);
    }

    private string ResolveThumbnailPath(string thumbnailPath)
    {
        return System.IO.Path.IsPathRooted(thumbnailPath)
            ? thumbnailPath
            : System.IO.Path.Combine(_storageFolder, thumbnailPath);
    }
}
