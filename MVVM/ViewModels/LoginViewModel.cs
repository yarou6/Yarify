using MVVM.Models.Auth;
using MVVM.Services;
using MVVM.Tools;

namespace MVVM.ViewModels;

public class LoginViewModel : BaseVM
{
    private readonly AuthSessionService _authSessionService;
    private readonly Func<AuthResponseDto, Task> _onAuthSuccess;
    private readonly Action _openRegister;

    private string _login = string.Empty;
    private string _password = string.Empty;
    private bool _rememberMe = true;
    private string _status = string.Empty;
    private bool _isBusy;

    public LoginViewModel(
        AuthSessionService authSessionService,
        Func<AuthResponseDto, Task> onAuthSuccess,
        Action openRegister)
    {
        _authSessionService = authSessionService;
        _onAuthSuccess = onAuthSuccess;
        _openRegister = openRegister;

        OpenRegisterCommand = new RelayCommand(OpenRegister, () => !IsBusy);
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
    }

    public string Login
    {
        get => _login;
        set => SetProperty(ref _login, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
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

    public RelayCommand OpenRegisterCommand { get; }
    public AsyncRelayCommand LoginCommand { get; }

    // Переключает раздел или состояние интерфейса.
    private void OpenRegister()
    {
        _openRegister();
    }

    // Выполняет внутреннюю логику метода.
    private async Task LoginAsync()
    {
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

    // Обрабатывает событие и запускает нужное действие.
    private void RaiseCommandCanExecuteChanged()
    {
        OpenRegisterCommand.RaiseCanExecuteChanged();
        LoginCommand.RaiseCanExecuteChanged();
    }
}
