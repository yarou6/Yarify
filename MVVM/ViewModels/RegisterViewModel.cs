using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MVVM.Models.Auth;
using MVVM.Services;

namespace MVVM.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthSessionService _authSessionService;
    private readonly Func<AuthResponseDto, Task> _onAuthSuccess;
    private readonly Action _backToLogin;

    [ObservableProperty]
    private string _login = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public RegisterViewModel(
        AuthSessionService authSessionService,
        Func<AuthResponseDto, Task> onAuthSuccess,
        Action backToLogin)
    {
        _authSessionService = authSessionService;
        _onAuthSuccess = onAuthSuccess;
        _backToLogin = backToLogin;
    }

    [RelayCommand]
    private void BackToLogin()
    {
        if (IsBusy)
            return;

        _backToLogin();
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Status = "Регистрируем...";

        var payload = new RegisterRequestDto
        {
            Login = Login,
            Email = Email,
            DisplayName = DisplayName,
            Password = Password,
            ConfirmPassword = ConfirmPassword
        };

        var (data, error) = await _authSessionService.ApiClient.RegisterAsync(payload);
        if (data is null)
        {
            Status = $"Ошибка регистрации: {error}";
            IsBusy = false;
            return;
        }

        Password = string.Empty;
        ConfirmPassword = string.Empty;
        await _onAuthSuccess(data);
        IsBusy = false;
    }
}
