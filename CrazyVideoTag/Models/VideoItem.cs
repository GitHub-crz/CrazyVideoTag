using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrazyVideoTag.Models;

public sealed class VideoItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string? _thumbnailPath;
    private string? _customCoverPath;
    private string? _thumbnailError;
    private bool _isGeneratingThumbnail;
    private TimeSpan? _duration;
    private int? _width;
    private int? _height;

    public string Path { get; init; } = string.Empty;
    public string Title => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;
    public DateTime ModifiedAt { get; init; }
    public long Size { get; init; }

    public List<string> TagIds { get; } = [];
    public List<string> ActorIds { get; } = [];

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string? ThumbnailPath
    {
        get => _thumbnailPath;
        set
        {
            if (SetField(ref _thumbnailPath, value))
            {
                OnPropertyChanged(nameof(EffectiveThumbnailPath));
            }
        }
    }

    public string? CustomCoverPath
    {
        get => _customCoverPath;
        set
        {
            if (SetField(ref _customCoverPath, value))
            {
                OnPropertyChanged(nameof(EffectiveThumbnailPath));
            }
        }
    }

    public string? EffectiveThumbnailPath => !string.IsNullOrWhiteSpace(CustomCoverPath) ? CustomCoverPath : ThumbnailPath;

    public string? ThumbnailError
    {
        get => _thumbnailError;
        set => SetField(ref _thumbnailError, value);
    }

    public bool IsGeneratingThumbnail
    {
        get => _isGeneratingThumbnail;
        set => SetField(ref _isGeneratingThumbnail, value);
    }

    public TimeSpan? Duration
    {
        get => _duration;
        set
        {
            if (SetField(ref _duration, value))
            {
                OnPropertyChanged(nameof(DurationText));
                OnPropertyChanged(nameof(InfoText));
            }
        }
    }

    public string SizeText => FormatSize(Size);
    public string DurationText => Duration is null ? "--:--" : FormatDuration(Duration.Value);

    /// <summary>分辨率简称：取宽高中较小的数值加 P，1920×1080 与 1080×1920 均显示 <c>1080P</c>。</summary>
    public string ResolutionText => Width is > 0 && Height is > 0 ? $"{Math.Min(Width.Value, Height.Value)}P" : "--";

    /// <summary>卡片信息行：日期时间 · 大小 · 时长 · 分辨率。</summary>
    public string InfoText => $"{FormatModifiedAt(ModifiedAt)} · {SizeText} · {DurationText} · {ResolutionText}";

    public int? Width
    {
        get => _width;
        set
        {
            if (SetField(ref _width, value))
            {
                OnPropertyChanged(nameof(ResolutionText));
                OnPropertyChanged(nameof(InfoText));
            }
        }
    }

    public int? Height
    {
        get => _height;
        set
        {
            if (SetField(ref _height, value))
            {
                OnPropertyChanged(nameof(ResolutionText));
                OnPropertyChanged(nameof(InfoText));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshTags()
    {
        OnPropertyChanged(nameof(TagIds));
        OnPropertyChanged(nameof(ActorIds));
    }

    /// <summary>
    /// 压缩修改时间：年份固定两位、月日去掉前导零，形如 <c>26-6-17 12:34</c>。
    /// </summary>
    private static string FormatModifiedAt(DateTime modifiedAt)
    {
        return $"{modifiedAt.Year % 100}-{modifiedAt.Month}-{modifiedAt.Day} {modifiedAt:HH:mm}";
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{value:0} {units[unitIndex]}" : $"{value:0.#} {units[unitIndex]}";
    }

    /// <summary>时长：跨小时用 <c>h:mm:ss</c>，一小时以内用 <c>mm:ss</c>。</summary>
    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
