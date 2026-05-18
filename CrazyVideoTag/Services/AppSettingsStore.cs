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
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options);
        return settings ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, Options);
    }
}
