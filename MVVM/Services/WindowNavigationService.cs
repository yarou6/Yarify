using Avalonia.Controls;
using MVVM.Models.Auth;
using MVVM.ViewModels;
using MVVM.Views;

namespace MVVM.Services;

public sealed class WindowNavigationService : INavigationService
{
    private readonly AuthSessionService _authSessionService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly PlayerSettingsStore _playerSettingsStore;
    private readonly Func<AuthResponseDto, Task> _onAuthSuccess;
    private readonly Func<Task> _onLogout;
    private readonly Action<Window> _showWindow;

    public WindowNavigationService(
        AuthSessionService authSessionService,
        IAudioPlayerService audioPlayerService,
        PlayerSettingsStore playerSettingsStore,
        Func<AuthResponseDto, Task> onAuthSuccess,
        Func<Task> onLogout,
        Action<Window> showWindow)
    {
        _authSessionService = authSessionService;
        _audioPlayerService = audioPlayerService;
        _playerSettingsStore = playerSettingsStore;
        _onAuthSuccess = onAuthSuccess;
        _onLogout = onLogout;
        _showWindow = showWindow;
    }

    public void OpenLogin()
    {
        var vm = new LoginViewModel(_authSessionService, _onAuthSuccess, OpenRegister);
        _showWindow(new LoginWindow { DataContext = vm });
    }

    public void OpenRegister()
    {
        var vm = new RegisterViewModel(_authSessionService, _onAuthSuccess, OpenLogin);
        _showWindow(new RegisterWindow { DataContext = vm });
    }

    public void OpenMain(AuthResponseDto authData)
    {
        var vm = new MainWindowViewModel(_authSessionService, authData, _audioPlayerService, _playerSettingsStore, _onLogout);
        _showWindow(new MainWindow { DataContext = vm });
    }
}
