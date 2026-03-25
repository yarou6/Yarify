using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Models.Auth;
using MVVM.Services;

namespace MVVM.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthSessionService _authSessionService;
    private readonly Func<AuthResponseDto, Task> _onAuthSuccess;
    private readonly Action _openRegister;

    [ObservableProperty]
    private string _login = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(
        AuthSessionService authSessionService,
        Func<AuthResponseDto, Task> onAuthSuccess,
        Action openRegister)
    {
        _authSessionService = authSessionService;
        _onAuthSuccess = onAuthSuccess;
        _openRegister = openRegister;
    }

    [RelayCommand]
    private void OpenRegister()
    {
        if (IsBusy)
            return;

        _openRegister();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Status = "Входим...";

        var (data, error) = await _authSessionService.ApiClient.LoginAsync(Login, Password, RememberMe);
        if (data is null)
        {
            Status = $"Ошибка входа: {error}";
            IsBusy = false;
            return;
        }

        Password = string.Empty;
        await _onAuthSuccess(data);
        IsBusy = false;
    }
}
