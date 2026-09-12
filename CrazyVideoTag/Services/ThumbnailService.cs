using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CrazyVideoTag.Models;

namespace CrazyVideoTag.Services;

public sealed record ThumbnailProgress(int Completed, int Total, string CurrentPath);
public sealed record ThumbnailResult(bool Success, string? ThumbnailPath, string? Error);

public sealed class ThumbnailService
{
    private const int ThumbnailWidth = 240;
    private const int ThumbnailHeight = 150;
    private string _storageFolder = AppContext.BaseDirectory;
    private string ThumbnailDirectory => System.IO.Path.Combine(_storageFolder, "thumbs");

    public void SetStorageFolder(string storageFolder)
    {
        _storageFolder = string.IsNullOrWhiteSpace(storageFolder) ? AppContext.BaseDirectory : storageFolder;
    }

    private static string PreviewDirectory => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CrazyVideoTagPreviews");

    public string? TryGetCachedPreviewPath(VideoItem video, double positionPercent)
    {
        var previewPath = GetPreviewPath(video, positionPercent);
        return File.Exists(previewPath) ? previewPath : null;
    }

    private static string GetPreviewPath(VideoItem video, double positionPercent)
    {
        var info = new FileInfo(video.Path);
        var normalizedPercent = (int)Math.Clamp(positionPercent, 1, 99);
        var key = $"{video.Path}|{normalizedPercent}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return System.IO.Path.Combine(PreviewDirectory, hash + ".jpg");
    }

    public async Task<string?> GeneratePreviewFrameAsync(VideoItem video, AppState state, double positionPercent, CancellationToken cancellationToken)
    {
        var cachedPath = TryGetCachedPreviewPath(video, positionPercent);
        if (cachedPath is not null)
        {
            return cachedPath;
        }

        Directory.CreateDirectory(PreviewDirectory);
        var previewPath = GetPreviewPath(video, positionPercent);
        var normalizedPercent = (int)Math.Clamp(positionPercent, 1, 99);

        var duration = await GetDurationAsync(state.FfprobePath, video.Path, cancellationToken);
        var timestamp = TimeSpan.FromSeconds(Math.Max(0.1, duration.TotalSeconds * normalizedPercent / 100));
        var ratio = (ThumbnailWidth / (double)ThumbnailHeight).ToString(CultureInfo.InvariantCulture);
        var filter = $"scale='if(gt(a,{ratio}),-1,{ThumbnailWidth})':'if(gt(a,{ratio}),{ThumbnailHeight},-1)',crop={ThumbnailWidth}:{ThumbnailHeight}";
        var arguments = $"-y -ss {FormatTime(timestamp)} -i {Quote(video.Path)} -frames:v 1 -vf {Quote(filter)} -q:v 4 -update 1 {Quote(previewPath)}";
        var result = await RunProcessAsync(state.FfmpegPath, arguments, cancellationToken);
        return result.ExitCode == 0 && File.Exists(previewPath) ? previewPath : null;
    }

    public async Task GenerateMissingAsync(IReadOnlyList<VideoItem> videos, AppState state, IProgress<ThumbnailProgress>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ThumbnailDirectory);
        var targets = videos.Where(video => string.IsNullOrWhiteSpace(video.ThumbnailPath) || !File.Exists(video.ThumbnailPath)).ToList();
        var completed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 2,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(targets, parallelOptions, async (video, ct) =>
        {
            await GenerateForVideoAsync(video, state, ct);
            var done = Interlocked.Increment(ref completed);
            progress?.Report(new ThumbnailProgress(done, targets.Count, video.Path));
        });
    }

    public async Task<ThumbnailResult> GenerateForVideoAsync(VideoItem video, AppState state, CancellationToken cancellationToken, double positionPercent = 50)
    {
        Directory.CreateDirectory(ThumbnailDirectory);
        video.IsGeneratingThumbnail = true;
        try
        {
            var info = new FileInfo(video.Path);
            // Use a fresh file for every generation so FFmpeg never overwrites a JPG still held by WPF.
            var thumbnailPath = GetThumbnailPath(video.Path, info);
            var duration = await GetDurationAsync(state.FfprobePath, video.Path, cancellationToken);
            video.Duration = duration;
            var normalizedPercent = Math.Clamp(positionPercent, 1, 99);
            var timestamp = TimeSpan.FromSeconds(Math.Max(0.1, duration.TotalSeconds * normalizedPercent / 100));
            var ratio = (ThumbnailWidth / (double)ThumbnailHeight).ToString(CultureInfo.InvariantCulture);
            var filter = $"scale='if(gt(a,{ratio}),-1,{ThumbnailWidth})':'if(gt(a,{ratio}),{ThumbnailHeight},-1)',crop={ThumbnailWidth}:{ThumbnailHeight}";
            var arguments = $"-y -ss {FormatTime(timestamp)} -i {Quote(video.Path)} -frames:v 1 -vf {Quote(filter)} -q:v 3 -update 1 {Quote(thumbnailPath)}";
            var ffmpeg = await RunProcessAsync(state.FfmpegPath, arguments, cancellationToken);
            if (ffmpeg.ExitCode != 0 || !File.Exists(thumbnailPath))
            {
                var error = string.IsNullOrWhiteSpace(ffmpeg.Error) ? "FFmpeg 生成封面失败。" : ffmpeg.Error.Trim();
                SetCache(state, video, info, thumbnailPath, error, video.Duration);
                return new ThumbnailResult(false, null, error);
            }

            SetCache(state, video, info, thumbnailPath, null, video.Duration, normalizedPercent);
            return new ThumbnailResult(true, thumbnailPath, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            video.ThumbnailError = ex.Message;
            return new ThumbnailResult(false, null, ex.Message);
        }
        finally
        {
            video.IsGeneratingThumbnail = false;
        }
    }

    public ThumbnailResult SetCoverFromPreview(VideoItem video, AppState state, double positionPercent)
    {
        var previewPath = TryGetCachedPreviewPath(video, positionPercent);
        if (previewPath is null)
        {
            return new ThumbnailResult(false, null, "该进度的预览图尚未生成。");
        }

        try
        {
            Directory.CreateDirectory(ThumbnailDirectory);
            var info = new FileInfo(video.Path);
            var coverPath = GetThumbnailPath(video.Path, info);
            File.Copy(previewPath, coverPath, overwrite: true);
            SetCache(state, video, info, coverPath, null, video.Duration, (int)Math.Clamp(positionPercent, 1, 99));
            return new ThumbnailResult(true, coverPath, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            video.ThumbnailError = ex.Message;
            return new ThumbnailResult(false, null, ex.Message);
        }
    }

    private void SetCache(AppState state, VideoItem video, FileInfo info, string thumbnailPath, string? error, TimeSpan? duration, double? positionPercent = null)
    {
        video.ThumbnailPath = error is null ? thumbnailPath : video.ThumbnailPath;
        video.ThumbnailError = error;
        state.ThumbnailCache[video.Path] = new ThumbnailCacheEntry
        {
            VideoPath = video.Path,
            ThumbnailPath = GetRelativeThumbnailPath(thumbnailPath),
            FileName = info.Name,
            FileSize = info.Length,
            LastWriteTicks = info.LastWriteTimeUtc.Ticks,
            DurationSeconds = duration?.TotalSeconds,
            PositionPercent = positionPercent,
            LastError = error
        };
    }

    private string GetThumbnailPath(string videoPath, FileInfo info)
    {
        var key = $"{videoPath}|{info.Name}|{Guid.NewGuid():N}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var name = Convert.ToHexString(bytes).ToLowerInvariant() + ".jpg";
        return System.IO.Path.Combine(ThumbnailDirectory, name);
    }

    private string GetRelativeThumbnailPath(string thumbnailPath)
    {
        return System.IO.Path.GetRelativePath(_storageFolder, thumbnailPath);
    }

    private static async Task<TimeSpan> GetDurationAsync(string ffprobePath, string videoPath, CancellationToken cancellationToken)
    {
        var arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {Quote(videoPath)}";
        var result = await RunProcessAsync(ffprobePath, arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? "ffprobe 读取时长失败。" : result.Error.Trim());
        }

        if (!double.TryParse(result.Output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
        {
            throw new InvalidOperationException("ffprobe 返回的视频时长无效。");
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        var output = new StringBuilder();
        var error = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) error.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"无法启动 {fileName}: {ex.Message}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception)
            {
            }
            throw;
        }

        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
