using System.IO;
using System.Text.Json;
using CrazyVideoTag.Models;

namespace CrazyVideoTag.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app-settings.json");

    public async Task<AppSettings> LoadAsync()
    {
        var settingsPath = File.Exists(_settingsPath) ? _settingsPath : TryImportSettingsFromSiblingVersion();
        if (settingsPath is null)
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options);
        return settings ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, Options);
    }

    private string? TryImportSettingsFromSiblingVersion()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        var publishDirectory = currentDirectory.Parent;
        if (publishDirectory is null)
        {
            return null;
        }

        var sourcePath = publishDirectory.EnumerateDirectories("CrazyVideoTag-v*-win-x64")
            .Where(directory => !string.Equals(directory.FullName, currentDirectory.FullName, StringComparison.OrdinalIgnoreCase))
            .Select(directory => System.IO.Path.Combine(directory.FullName, "app-settings.json"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (sourcePath is null)
        {
            return null;
        }

        try
        {
            File.Copy(sourcePath, _settingsPath, overwrite: false);
            return _settingsPath;
        }
        catch (IOException)
        {
            return sourcePath;
        }
        catch (UnauthorizedAccessException)
        {
            return sourcePath;
        }
    }
}
