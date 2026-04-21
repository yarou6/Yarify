using MVVM.Models.Auth;
using MVVM.Services;
using MVVM.Tools;

namespace MVVM.ViewModels;

public class RegisterViewModel : BaseVM
{
    private readonly AuthSessionService _authSessionService;
    private readonly Func<AuthResponseDto, Task> _onAuthSuccess;
    private readonly Action _backToLogin;

    private string _login = string.Empty;
    private string _email = string.Empty;
    private string _displayName = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _status = string.Empty;
    private bool _isBusy;

    public RegisterViewModel(
        AuthSessionService authSessionService,
        Func<AuthResponseDto, Task> onAuthSuccess,
        Action backToLogin)
    {
        _authSessionService = authSessionService;
        _onAuthSuccess = onAuthSuccess;
        _backToLogin = backToLogin;

        BackToLoginCommand = new RelayCommand(BackToLogin, () => !IsBusy);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => !IsBusy);
    }
    public string Login
    {
        get => _login;
        set => SetProperty(ref _login, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value, RaiseCommandCanExecuteChanged);
    }

    public RelayCommand BackToLoginCommand { get; }
    public AsyncRelayCommand RegisterCommand { get; }

    private void BackToLogin()
    {
        _backToLogin();
    }

    private async Task RegisterAsync()
    {
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

    private void RaiseCommandCanExecuteChanged()
    {
        BackToLoginCommand.RaiseCanExecuteChanged();
        RegisterCommand.RaiseCanExecuteChanged();
    }
}
