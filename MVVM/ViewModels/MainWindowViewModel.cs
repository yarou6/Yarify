using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Models.Auth;
using MVVM.Services;

namespace MVVM.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AuthSessionService _authSessionService;
    private readonly Func<Task> _onLogout;

    [ObservableProperty]
    private string _displayName = "Yarify User";

    [ObservableProperty]
    private string _roleTitle = "User";

    [ObservableProperty]
    private string _userIdText = "ID: -";

    [ObservableProperty]
    private string _status = "Вы в приложении.";

    public MainWindowViewModel(AuthSessionService authSessionService, AuthResponseDto authData, Func<Task> onLogout)
    {
        _authSessionService = authSessionService;
        _onLogout = onLogout;

        RoleTitle = $"Роль: {authData.RoleTitle}";
        UserIdText = $"ID: {authData.UserId}";
        DisplayName = $"Пользователь #{authData.UserId}";
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        Status = "Выходим...";

        var session = await _authSessionService.SessionStore.TryLoadAsync();
        if (session is not null && !string.IsNullOrWhiteSpace(session.RefreshToken))
            await _authSessionService.ApiClient.LogoutAsync(session.RefreshToken);

        await _authSessionService.SessionStore.ClearAsync();
        await _onLogout();
    }
}
