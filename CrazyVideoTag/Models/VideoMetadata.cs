namespace CrazyVideoTag.Models;

public sealed class VideoMetadata
{
    public string Path { get; set; } = string.Empty;
    public string? CustomCoverPath { get; set; }
    public List<string> TagIds { get; set; } = [];
    public List<string> ActorIds { get; set; } = [];
}
