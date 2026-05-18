namespace CrazyVideoTag.Models;

public sealed class ThumbnailCacheEntry
{
    public string VideoPath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long LastWriteTicks { get; set; }
    public long FileSize { get; set; }
    public double? DurationSeconds { get; set; }
    public string? LastError { get; set; }
}
