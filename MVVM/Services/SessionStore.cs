using System.Text.Json;
using MVVM.Models.Auth;

namespace MVVM.Services;

public sealed class SessionStore
{
    private readonly string _sessionFilePath;

    public SessionStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Yarify");
        Directory.CreateDirectory(dir);
        _sessionFilePath = Path.Combine(dir, "session.json");
    }

    public async Task<SessionSnapshot?> TryLoadAsync()
    {
        if (!File.Exists(_sessionFilePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_sessionFilePath);
            return JsonSerializer.Deserialize<SessionSnapshot>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(SessionSnapshot session)
    {
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_sessionFilePath, json);
    }

    public Task ClearAsync()
    {
        if (File.Exists(_sessionFilePath))
            File.Delete(_sessionFilePath);

        return Task.CompletedTask;
    }
}
