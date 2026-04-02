using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MVVM.Models.Auth;
using MVVM.Models.Playback;
using MVVM.Models.Profile;
using MVVM.Models.Subscriptions;

namespace MVVM.Services;

public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthApiClient(string baseUrl = "http://localhost:5048")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
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

        return await GetListAsync<TrackListItemDto>(url);
    }

    public async Task<(IReadOnlyList<GenreItemDto> Data, string? Error)> GetGenresAsync() =>
        await GetListAsync<GenreItemDto>("/api/player/genres");

    public async Task<(IReadOnlyList<TrackListItemDto> Data, string? Error)> GetLikedTracksAsync() =>
        await GetListAsync<TrackListItemDto>("/api/player/liked");

    public async Task<string?> LikeTrackAsync(int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Post, $"/api/player/liked/{songId}");

    public async Task<string?> UnlikeTrackAsync(int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Delete, $"/api/player/liked/{songId}");

    public async Task<(IReadOnlyList<QueueItemDto> Data, string? Error)> GetQueueAsync() =>
        await GetListAsync<QueueItemDto>("/api/player/queue");

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

    public async Task<(IReadOnlyList<TrackListItemDto> Data, string? Error)> GetPlaylistTracksAsync(int playlistId) =>
        await GetListAsync<TrackListItemDto>($"/api/player/playlists/{playlistId}/tracks");

    public async Task<string?> AddTrackToPlaylistAsync(int playlistId, int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Post, $"/api/player/playlists/{playlistId}/tracks/{songId}");

    public async Task<string?> RemoveTrackFromPlaylistAsync(int playlistId, int songId) =>
        await SendWithoutResponseBodyAsync(HttpMethod.Delete, $"/api/player/playlists/{playlistId}/tracks/{songId}");

    public async Task<(ProfileMeDto? Data, string? Error)> GetProfileMeAsync() =>
        await GetAsync<ProfileMeDto>("/api/profile/me");

    public string? ResolveAssetUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            return absolute.ToString();

        var baseUri = _httpClient.BaseAddress ?? new Uri("http://localhost:5048");
        return new Uri(baseUri, path).ToString();
    }

    public async Task<(ArtistDetailsDto? Data, string? Error)> GetArtistAsync(int artistUserId) =>
        await GetAsync<ArtistDetailsDto>($"/api/player/artists/{artistUserId}");

    public async Task<(AlbumDetailsDto? Data, string? Error)> GetAlbumAsync(int albumId) =>
        await GetAsync<AlbumDetailsDto>($"/api/player/albums/{albumId}");

    public async Task<(IReadOnlyList<SubscriptionPlanDto> Data, string? Error)> GetSubscriptionPlansAsync() =>
        await GetListAsync<SubscriptionPlanDto>("/api/subscriptions/plans");

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
}





