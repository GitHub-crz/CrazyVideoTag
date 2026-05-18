using System.IO;
using System.Text.Json;
using CrazyVideoTag.Models;

namespace CrazyVideoTag.Services;

public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private string _storageFolder = AppContext.BaseDirectory;

    public string StorageFolder
    {
        get => _storageFolder;
        set => _storageFolder = string.IsNullOrWhiteSpace(value) ? AppContext.BaseDirectory : value;
    }

    public string StatePath => System.IO.Path.Combine(StorageFolder, "video-tags.json");

    public async Task<AppState> LoadAsync()
    {
        if (!File.Exists(StatePath))
        {
            return new AppState();
        }

        await using var stream = File.OpenRead(StatePath);
        var state = await JsonSerializer.DeserializeAsync<AppState>(stream, Options);
        return state ?? new AppState();
    }

    public async Task SaveAsync(AppState state)
    {
        var directory = System.IO.Path.GetDirectoryName(StatePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(StatePath);
        await JsonSerializer.SerializeAsync(stream, state, Options);
    }
}
