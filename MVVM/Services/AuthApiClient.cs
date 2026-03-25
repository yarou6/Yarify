using System.Net.Http.Json;
using System.Text.Json;
using MVVM.Models.Auth;

namespace MVVM.Services;

public sealed class AuthApiClient
{
    private readonly HttpClient _httpClient;

    public AuthApiClient(string baseUrl = "http://localhost:5048")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
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

    private async Task<(TResponse? Data, string? Error)> PostJsonAsync<TRequest, TResponse>(string url, TRequest payload)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(text))
                    return (default, $"HTTP {(int)response.StatusCode}");

                return (default, text);
            }

            var model = await response.Content.ReadFromJsonAsync<TResponse>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return model is null ? (default, "Пустой ответ от API") : (model, null);
        }
        catch (Exception ex)
        {
            return (default, ex.Message);
        }
    }
}
