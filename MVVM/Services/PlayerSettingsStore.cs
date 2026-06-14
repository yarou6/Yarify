using System.Text.Json;
using MVVM.Models.Playback;

namespace MVVM.Services;

public sealed class PlayerSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public PlayerSettingsStore()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yarify");
        Directory.CreateDirectory(baseDir);
        _settingsPath = Path.Combine(baseDir, "player-settings.json");
    }

    // Готовит и возвращает нужные данные.
    public async Task<PlayerSettingsSnapshot> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
            return new PlayerSettingsSnapshot();

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var model = await JsonSerializer.DeserializeAsync<PlayerSettingsSnapshot>(stream, JsonOptions);
            return model ?? new PlayerSettingsSnapshot();
        }
        catch
        {
            return new PlayerSettingsSnapshot();
        }
    }

    // Обновляет состояние и приводит данные к нужному виду.
    public async Task SaveAsync(PlayerSettingsSnapshot snapshot)
    {
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions);
    }
}
