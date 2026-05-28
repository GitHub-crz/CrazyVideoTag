namespace CrazyVideoTag.Models;

public sealed class AppState
{
    public string? LastFolder { get; set; }
    public string FfmpegPath { get; set; } = "ffmpeg.exe";
    public string FfprobePath { get; set; } = "ffprobe.exe";
    public List<TagDefinition> Tags { get; set; } = [];
    public Dictionary<string, VideoMetadata> Videos { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ThumbnailCacheEntry> ThumbnailCache { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public VideoSortMode SortMode { get; set; } = VideoSortMode.ModifiedDesc;
}
