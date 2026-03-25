namespace MVVM.Services;

public sealed class AuthSessionService
{
    private readonly AuthApiClient _apiClient;
    private readonly SessionStore _sessionStore;

    public AuthSessionService(AuthApiClient apiClient, SessionStore sessionStore)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
    }

    public AuthApiClient ApiClient => _apiClient;
    public SessionStore SessionStore => _sessionStore;
}
