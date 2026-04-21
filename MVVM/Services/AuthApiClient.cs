using System.Net.Http.Headers;
using System.Net.Http.Json;
using Avalonia.Media.Imaging;
using System.Text;
using System.Text.Json;
using MVVM.Models.Auth;
using MVVM.Models.Library;
using MVVM.Models.Playback;
using MVVM.Models.Profile;
using MVVM.Models.Subscriptions;

namespace MVVM.Services;

public sealed class AuthApiClient
{
    public const string DefaultBaseUrl = "http://localhost:5048";
    private readonly HttpClient _httpClient;
    private static readonly object FileLookupSync = new();
    private static readonly Dictionary<string, string?> FileLookupCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthApiClient(string? baseUrl = null)
    {
        var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl;
        if (!Uri.TryCreate(resolvedBaseUrl, UriKind.Absolute, out var baseUri))
            baseUri = new Uri(DefaultBaseUrl);

        _httpClient = new HttpClient
        {
            BaseAddress = baseUri
        };
    }

    public void SetAccessToken(string? accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<(AuthResponseDto? Data, string? Error)> LoginAsync(string login, string password, bool rememberMe)
    {
        var endpoint = rememberMe ? "/api/auth/login-remember-me" : "/api/auth/login";
        var payload = new LoginRequestDto { Login = login, Password = password };
        return await PostJsonAsync<LoginRequestDto, AuthResponseDto>(endpoint, payload);
    }

    public async Task<(AuthResponseDto? Data, string? Error)> RegisterAsync(RegisterRequestDto payload)
    {
        return await PostJsonAsync<RegisterRequestDto, AuthResponseDto>("/api/auth/register", payload);
    }

    public async Task<(AuthResponseDto? Data, string? Error)> RefreshAsync(string refreshToken)
    {
        var payload = new RefreshTokenRequestDto { RefreshToken = refreshToken };
        return await PostJsonAsync<RefreshTokenRequestDto, AuthResponseDto>("/api/session/refresh", payload);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var payload = new RefreshTokenRequestDto { RefreshToken = refreshToken };
        await _httpClient.PostAsJsonAsync("/api/logout", payload);
    }

    public async Task<(IReadOnlyList<TrackListItemDto> Data, string? Error)> GetTracksAsync(string? query = null, string? genre = null, string sort = "title")
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query))
            parts.Add($"query={Uri.EscapeDataString(query)}");
        if (!string.IsNullOrWhiteSpace(genre) && !string.Equals(genre, "Все", StringComparison.OrdinalIgnoreCase))
            parts.Add($"genre={Uri.EscapeDataString(genre)}");
        if (!string.IsNullOrWhiteSpace(sort))
            parts.Add($"sort={Uri.EscapeDataString(sort)}");

        var url = "/api/player/tracks";
        if (parts.Count > 0)
            url += "?" + string.Join("&", parts);

        var (tracks, error) = await GetListAsync<TrackListItemDto>(url);
        NormalizeTracks(tracks);
        return (tracks, error);
    }

    public async Task<(IReadOnlyList<ArtistSearchItemDto> Data, string? Error)> SearchArtistsAsync(string query, int take = 50)
    {
        var needle = query?.Trim();
        if (string.IsNullOrWhiteSpace(needle))
            return (Array.Empty<ArtistSearchItemDto>(), null);

        var normalizedTake = Math.Clamp(take, 1, 100);
        var url = $"/api/player/search?query={Uri.EscapeDataString(needle)}&take={normalizedTake}";
        var (payload, error) = await GetAsync<SearchArtistsResponseDto>(url);
        if (payload is null)
            return (Array.Empty<ArtistSearchItemDto>(), error);

        var items = payload.Artists
            .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase)
            .Select(a =>
            {
                var avatarPath = ResolveMediaDisplaySource(a.AvatarPath);
                return new ArtistSearchItemDto
                {
                    ArtistUserId = a.ArtistUserId,
                    ArtistName = a.ArtistName,
                    AvatarPath = avatarPath,
                    AvatarBitmap = TryLoadBitmap(avatarPath),
                    TracksCount = a.TracksCount
                };
            })
            .ToList();

        return (items, error);
    }

    public async Task<(IReadOnlyList<GenreItemDto> Data, string? Error)> GetGenresAsync() =>
        await GetListAsync<GenreItemDto>("/api/player/genres");

    public async Task<(IReadOnlyList<TrackListItemDto> Data, string? Error)> GetLikedTracksAsync()
    {
        var (tracks, error) = await GetListAsync<TrackListItemDto>("/api/player/liked");
        NormalizeTracks(tracks);
        return (tracks, error);
    }

    public async Task<string?> LikeTrackAsync(int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Post, $"/api/player/liked/{songId}");

    public async Task<string?> UnlikeTrackAsync(int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Delete, $"/api/player/liked/{songId}");

    public async Task<(IReadOnlyList<QueueItemDto> Data, string? Error)> GetQueueAsync()
    {
        var (items, error) = await GetListAsync<QueueItemDto>("/api/player/queue");
        foreach (var item in items.Where(i => i.Track is not null))
            NormalizeTracks(new[] { item.Track });

        return (items, error);
    }

    public async Task<string?> AddToQueueAsync(int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Post, $"/api/player/queue/{songId}");

    public async Task<string?> RemoveFromQueueAsync(long queueId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Delete, $"/api/player/queue/{queueId}");

    public async Task<string?> MoveQueueUpAsync(long queueId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Post, $"/api/player/queue/{queueId}/move-up");

    public async Task<string?> MoveQueueDownAsync(long queueId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Post, $"/api/player/queue/{queueId}/move-down");

    public async Task<string?> ClearQueueAsync() =>
        await SendWithoutResponseBodyAsync(HttpMethod.Post, "/api/player/queue/clear");

    public async Task<(IReadOnlyList<PlaylistListItemDto> Data, string? Error)> GetPlaylistsAsync() =>
        await GetListAsync<PlaylistListItemDto>("/api/player/playlists");

    public async Task<(PlaylistListItemDto? Data, string? Error)> CreatePlaylistAsync(CreatePlaylistRequestDto payload) =>
        await PostJsonAsync<CreatePlaylistRequestDto, PlaylistListItemDto>("/api/player/playlists", payload);

    public async Task<(PlaylistListItemDto? Data, string? Error)> UpdatePlaylistAsync(int playlistId, UpdatePlaylistRequestDto payload) =>
        await PutJsonAsync<UpdatePlaylistRequestDto, PlaylistListItemDto>($"/api/player/playlists/{playlistId}", payload);
    public async Task<string?> DeletePlaylistAsync(int playlistId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Delete, $"/api/player/playlists/{playlistId}");

    public async Task<(IReadOnlyList<TrackListItemDto> Data, string? Error)> GetPlaylistTracksAsync(int playlistId)
    {
        var (tracks, error) = await GetListAsync<TrackListItemDto>($"/api/player/playlists/{playlistId}/tracks");
        NormalizeTracks(tracks);
        return (tracks, error);
    }

    public async Task<string?> AddTrackToPlaylistAsync(int playlistId, int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Post, $"/api/player/playlists/{playlistId}/tracks/{songId}");

    public async Task<string?> RemoveTrackFromPlaylistAsync(int playlistId, int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Delete, $"/api/player/playlists/{playlistId}/tracks/{songId}");

    public async Task<(ProfileMeDto? Data, string? Error)> GetProfileMeAsync() =>
        await GetAsync<ProfileMeDto>("/api/profile/me");

    public async Task<(ProfileMeDto? Data, string? Error)> UpdateProfileMeAsync(UpdateProfileRequestDto payload) =>
        await PutJsonAsync<UpdateProfileRequestDto, ProfileMeDto>("/api/profile/me", payload);

    public async Task<string?> UploadProfileAvatarAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return "Файл аватара не найден.";

            await using var stream = File.OpenRead(filePath);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync("/api/profile/me/avatar", content);
            if (response.IsSuccessStatusCode)
                return null;

            return await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<string?> ChangePasswordAsync(ChangePasswordRequestDto payload) =>
        await PatchJsonWithoutResponseAsync("/api/users-panel/change-password", payload);

    public async Task<string?> SetArtistRoleAsync(UsersPanelRoleRequestDto payload) =>
        await PatchJsonWithoutResponseAsync("/api/users-panel/role", payload);

    public async Task<(IReadOnlyList<FollowingArtistItemDto> Data, string? Error)> GetFollowingArtistsAsync() =>
        await GetListAsync<FollowingArtistItemDto>("/api/player/social/following");

    public async Task<(UserSubscriptionDto? Data, string? Error)> GetMySubscriptionAsync() =>
        await GetAsync<UserSubscriptionDto>("/api/subscriptions/me");

    public async Task<(UserSubscriptionDto? Data, string? Error)> ChangeMySubscriptionAsync(ChangeSubscriptionRequestDto payload) =>
        await PutJsonAsync<ChangeSubscriptionRequestDto, UserSubscriptionDto>("/api/subscriptions/me", payload);

    public string? ResolveAssetUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalizedInput = path.Trim();
        if (Path.IsPathRooted(normalizedInput) && File.Exists(normalizedInput))
            return new Uri(normalizedInput).AbsoluteUri;

        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var baseUri = _httpClient.BaseAddress ?? new Uri(DefaultBaseUrl);
        var normalized = normalizedInput.Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
            normalized = "/" + normalized;

        return new Uri(baseUri, normalized).ToString();
    }

    public async Task<(ArtistDetailsDto? Data, string? Error)> GetArtistAsync(int artistUserId)
    {
        var (artist, error) = await GetAsync<ArtistDetailsDto>($"/api/player/artists/{artistUserId}");
        if (artist is null)
            return (null, error);

        artist.AvatarPath = ResolveMediaDisplaySource(artist.AvatarPath);
        artist.AvatarBitmap = TryLoadBitmap(artist.AvatarPath);
        NormalizeTracks(artist.TopTracks);
        foreach (var album in artist.Albums)
        {
            album.CoverPath = ResolveMediaDisplaySource(album.CoverPath);
            album.CoverBitmap = TryLoadBitmap(album.CoverPath);
        }

        return (artist, error);
    }

    public async Task<(FollowArtistStateDto? Data, string? Error)> FollowArtistAsync(int artistUserId) =>
        await PostJsonAsync<object, FollowArtistStateDto>($"/api/player/artists/{artistUserId}/follow", new { });

    public async Task<(FollowArtistStateDto? Data, string? Error)> UnfollowArtistAsync(int artistUserId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/player/artists/{artistUserId}/follow");
            if (!response.IsSuccessStatusCode)
                return (null, await ReadErrorAsync(response));

            var model = await response.Content.ReadFromJsonAsync<FollowArtistStateDto>(JsonOptions);
            return model is null ? (null, "Пустой ответ от API") : (model, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public async Task<(AlbumDetailsDto? Data, string? Error)> GetAlbumAsync(int albumId)
    {
        var (album, error) = await GetAsync<AlbumDetailsDto>($"/api/player/albums/{albumId}");
        if (album is null)
            return (null, error);

        album.CoverPath = ResolveMediaDisplaySource(album.CoverPath);
        album.CoverBitmap = TryLoadBitmap(album.CoverPath);
        NormalizeTracks(album.Tracks);
        return (album, error);
    }

    public async Task<(long? EventId, string? Error)> StartListeningEventAsync(int songId, string sourceType = "Direct", int? sourceId = null, DateTime? startedAt = null)
    {
        var payload = new StartListeningEventRequestDto
        {
            SongId = songId,
            SourceType = sourceType,
            SourceId = sourceId,
            StartedAt = startedAt
        };
        var (data, error) = await PostJsonAsync<StartListeningEventRequestDto, ListeningEventCreatedDto>("/api/listening-events/start", payload);
        return (data?.EventId, error);
    }

    public async Task<string?> ReportListeningProgressAsync(long eventId, int playedMs, DateTime? endedAt = null)
    {
        var payload = new ListeningEventProgressRequestDto
        {
            PlayedMs = Math.Max(0, playedMs),
            EndedAt = endedAt
        };
        return await PatchJsonWithoutResponseAsync($"/api/listening-events/{eventId}/progress", payload);
    }

    public async Task<string?> CompleteListeningEventAsync(long eventId, int playedMs, bool completed, DateTime? endedAt = null)
    {
        var payload = new CompleteListeningEventRequestDto
        {
            PlayedMs = Math.Max(0, playedMs),
            Completed = completed,
            EndedAt = endedAt
        };
        return await PostJsonWithoutResponseAsync($"/api/listening-events/{eventId}/complete", payload);
    }

    public async Task<(IReadOnlyList<SubscriptionPlanDto> Data, string? Error)> GetSubscriptionPlansAsync() =>
        await GetListAsync<SubscriptionPlanDto>("/api/subscriptions/plans");

    public async Task<(ManageAlbumDto? Data, string? Error)> CreateMyAlbumAsync(CreateAlbumRequestDto payload) =>
        await PostJsonAsync<CreateAlbumRequestDto, ManageAlbumDto>("/api/library/my/albums", payload);

    public async Task<(ManageSongDto? Data, string? Error)> CreateMySongAsync(CreateSongRequestDto payload) =>
        await PostJsonAsync<CreateSongRequestDto, ManageSongDto>("/api/library/my/songs", payload);

    public async Task<string?> UploadSongAudioAsync(int songId, string filePath) =>
        await UploadLibraryFileAsync($"/api/library/my/songs/{songId}/upload-audio", filePath);

    public async Task<string?> UploadSongCoverAsync(int songId, string filePath) =>
        await UploadLibraryFileAsync($"/api/library/my/songs/{songId}/upload-cover", filePath);

    public async Task<string?> UploadAlbumCoverAsync(int albumId, string filePath) =>
        await UploadLibraryFileAsync($"/api/library/my/albums/{albumId}/upload-cover", filePath);

    private async Task<(IReadOnlyList<T> Data, string? Error)> GetListAsync<T>(string url)
    {
        var (data, error) = await GetAsync<List<T>>(url);
        IReadOnlyList<T> result = data ?? new List<T>();
        return (result, error);
    }

    private async Task<(TResponse? Data, string? Error)> GetAsync<TResponse>(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return (default, await ReadErrorAsync(response));

            var model = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
            return model is null ? (default, "Пустой ответ от API") : (model, null);
        }
        catch (Exception ex)
        {
            return (default, ex.Message);
        }
    }

    private async Task<(TResponse? Data, string? Error)> PutJsonAsync<TRequest, TResponse>(string url, TRequest payload)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response);
                return (default, error);
            }

            var model = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
            return model is null ? (default, "Пустой ответ от API") : (model, null);
        }
        catch (Exception ex)
        {
            return (default, ex.Message);
        }
    }
    private async Task<(TResponse? Data, string? Error)> PostJsonAsync<TRequest, TResponse>(string url, TRequest payload)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response);
                return (default, error);
            }

            var model = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
            return model is null ? (default, "Пустой ответ от API") : (model, null);
        }
        catch (Exception ex)
        {
            return (default, ex.Message);
        }
    }

    private async Task<string?> SendWithoutResponseBodyAsync(HttpMethod method, string url)
    {
        try
        {
            using var req = new HttpRequestMessage(method, url);
            var response = await _httpClient.SendAsync(req);
            if (response.IsSuccessStatusCode)
                return null;

            return await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private async Task<string?> PatchJsonWithoutResponseAsync<TRequest>(string url, TRequest payload)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = JsonContent.Create(payload)
            };

            var response = await _httpClient.SendAsync(req);
            if (response.IsSuccessStatusCode)
                return null;

            return await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private async Task<string?> PostJsonWithoutResponseAsync<TRequest>(string url, TRequest payload)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            if (response.IsSuccessStatusCode)
                return null;

            return await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private async Task<string?> UploadLibraryFileAsync(string endpoint, string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return "Файл не найден.";

            await using var stream = File.OpenRead(filePath);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync(endpoint, content);
            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text))
            return $"HTTP {(int)response.StatusCode}";

        try
        {
            var apiError = JsonSerializer.Deserialize<ApiErrorResponse>(text, JsonOptions);
            if (apiError is null)
                return text;

            if (apiError.Errors is { Count: > 0 })
            {
                var first = apiError.Errors.FirstOrDefault().Value?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first))
                    return first;
            }

            return string.IsNullOrWhiteSpace(apiError.Message) ? text : apiError.Message;
        }
        catch
        {
            return text;
        }
    }

    private sealed class ApiErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, string[]>? Errors { get; set; }
    }

    private void NormalizeTracks(IEnumerable<TrackListItemDto> tracks)
    {
        foreach (var track in tracks)
        {
            track.CoverPath = ResolveMediaDisplaySource(track.CoverPath);
            track.CoverBitmap = TryLoadBitmap(track.CoverPath);
            track.LocalPath = ResolvePlaybackSource(track.LocalPath);
        }
    }

    private string? ResolveMediaDisplaySource(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalizedInput = path.Trim();
        if (Path.IsPathRooted(normalizedInput) && File.Exists(normalizedInput))
            return normalizedInput;

        if (Path.IsPathRooted(normalizedInput))
        {
            var repairedPath = TryRepairMojibakePath(normalizedInput);
            if (!string.IsNullOrWhiteSpace(repairedPath) && File.Exists(repairedPath))
                return repairedPath;

            var byName = TryResolveByFileName(normalizedInput);
            if (!string.IsNullOrWhiteSpace(byName) && File.Exists(byName))
                return byName;

            return null;
        }

        if (Uri.TryCreate(normalizedInput, UriKind.Absolute, out var absolute))
        {
            if (absolute.IsFile && File.Exists(absolute.LocalPath))
                return absolute.LocalPath;

            if (absolute.IsFile)
            {
                var repairedPath = TryRepairMojibakePath(absolute.LocalPath);
                if (!string.IsNullOrWhiteSpace(repairedPath) && File.Exists(repairedPath))
                    return repairedPath;
            }

            return absolute.ToString();
        }

        var local = TryResolveLocalUploadPath(normalizedInput);
        if (!string.IsNullOrWhiteSpace(local) && File.Exists(local))
            return local;

        return ResolveAssetUrl(normalizedInput);
    }

    private string? ResolvePlaybackSource(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalizedInput = path.Trim();
        if (Path.IsPathRooted(normalizedInput) && File.Exists(normalizedInput))
            return normalizedInput;

        if (Uri.TryCreate(normalizedInput, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var local = TryResolveLocalUploadPath(normalizedInput);
        if (!string.IsNullOrWhiteSpace(local) && File.Exists(local))
            return local;

        return ResolveAssetUrl(normalizedInput);
    }

    private static string? TryResolveLocalUploadPath(string relativeOrName)
    {
        var normalized = relativeOrName.TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var starts = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var start in starts)
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var direct = Path.Combine(dir.FullName, "API", "wwwroot", normalized);
                if (File.Exists(direct))
                    return direct;

                var uploadsRoot = Path.Combine(dir.FullName, "API", "wwwroot", "uploads");
                var byName = Path.Combine(uploadsRoot, Path.GetFileName(normalized));
                if (File.Exists(byName))
                    return byName;

                foreach (var folder in new[] { "avatars", "cover", "covers", "albums", "tracks", "songs", "music" })
                {
                    var candidate = Path.Combine(uploadsRoot, folder, Path.GetFileName(normalized));
                    if (File.Exists(candidate))
                        return candidate;
                }

                var musicRootCandidate = Path.Combine(dir.FullName, "API", "wwwroot", "music", Path.GetFileName(normalized));
                if (File.Exists(musicRootCandidate))
                    return musicRootCandidate;

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static string? TryResolveByFileName(string rawPath)
    {
        var fileName = Path.GetFileName(rawPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        lock (FileLookupSync)
        {
            if (FileLookupCache.TryGetValue(fileName, out var cached))
                return cached;
        }

        var roots = new List<string>();
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (!string.IsNullOrWhiteSpace(pictures)) roots.Add(pictures);
        if (!string.IsNullOrWhiteSpace(music)) roots.Add(music);

        var appBase = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(appBase))
        {
            var dir = new DirectoryInfo(appBase);
            while (dir is not null)
            {
                roots.Add(Path.Combine(dir.FullName, "API", "wwwroot", "uploads"));
                dir = dir.Parent;
            }
        }

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(root))
                    continue;

                var found = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(found))
                {
                    lock (FileLookupSync)
                        FileLookupCache[fileName] = found;
                    return found;
                }
            }
            catch
            {
                // ignore and continue scanning other roots
            }
        }

        lock (FileLookupSync)
            FileLookupCache[fileName] = null;

        return null;
    }

    private static string? TryRepairMojibakePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var cp1251 = TryGetEncoding(1251);
        var cp866 = TryGetEncoding(866);

        var candidates = new List<string?>();
        if (cp1251 is not null)
            candidates.Add(TryReencode(path, cp1251, Encoding.UTF8));
        candidates.Add(TryReencode(path, Encoding.Latin1, Encoding.UTF8));
        if (cp1251 is not null && cp866 is not null)
            candidates.Add(TryReencode(path, cp1251, cp866));

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) &&
                !string.Equals(candidate, path, StringComparison.Ordinal) &&
                Path.IsPathRooted(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? TryReencode(string input, Encoding source, Encoding target)
    {
        try
        {
            var bytes = source.GetBytes(input);
            return target.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static Encoding? TryGetEncoding(int codePage)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(codePage);
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? TryLoadBitmap(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile && File.Exists(uri.LocalPath))
                    return new Bitmap(uri.LocalPath);

                return null;
            }

            if (File.Exists(source))
                return new Bitmap(source);

            return null;
        }
        catch
        {
            return null;
        }
    }}

public sealed class StartListeningEventRequestDto
{
    public int SongId { get; set; }
    public string? SourceType { get; set; }
    public int? SourceId { get; set; }
    public DateTime? StartedAt { get; set; }
}

public sealed class ListeningEventProgressRequestDto
{
    public int PlayedMs { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class CompleteListeningEventRequestDto
{
    public int PlayedMs { get; set; }
    public bool Completed { get; set; } = true;
    public DateTime? EndedAt { get; set; }
}

public sealed class ListeningEventCreatedDto
{
    public long EventId { get; set; }
    public DateTime StartedAt { get; set; }
}

public sealed class SearchArtistsResponseDto
{
    public IReadOnlyList<SearchArtistItemDto> Artists { get; set; } = Array.Empty<SearchArtistItemDto>();
}

public sealed class SearchArtistItemDto
{
    public int ArtistUserId { get; set; }
    public string ArtistName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public int TracksCount { get; set; }
}











