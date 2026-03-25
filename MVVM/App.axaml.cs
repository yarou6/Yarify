using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MVVM.Models.Auth;
using MVVM.Services;
using MVVM.ViewModels;
using MVVM.Views;
using System.Linq;

namespace MVVM;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private AuthSessionService? _authSessionService;
    private Window? _currentWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            _desktop = desktop;
            var authApiClient = new AuthApiClient("http://localhost:5048");
            var sessionStore = new SessionStore();
            _authSessionService = new AuthSessionService(authApiClient, sessionStore);

            StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void StartAsync()
    {
        if (_desktop is null || _authSessionService is null)
            return;

        var authData = await TryRestoreSessionAsync(_authSessionService);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (authData is null)
            {
                OpenLoginWindow();
                return;
            }

            OpenMainWindow(authData);
        });
    }

    private async Task<AuthResponseDto?> TryRestoreSessionAsync(AuthSessionService authSessionService)
    {
        var session = await authSessionService.SessionStore.TryLoadAsync();
        if (session is null || string.IsNullOrWhiteSpace(session.RefreshToken))
            return null;

        var (data, _) = await authSessionService.ApiClient.RefreshAsync(session.RefreshToken);
        if (data is null)
        {
            await authSessionService.SessionStore.ClearAsync();
            return null;
        }

        await authSessionService.SessionStore.SaveAsync(MapSession(data));
        return data;
    }

    private async Task HandleAuthSuccessAsync(AuthResponseDto authData)
    {
        if (_authSessionService is null)
            return;

        await _authSessionService.SessionStore.SaveAsync(MapSession(authData));
        await Dispatcher.UIThread.InvokeAsync(() => OpenMainWindow(authData));
    }

    private void OpenLoginWindow()
    {
        if (_authSessionService is null)
            return;

        var vm = new LoginViewModel(_authSessionService, HandleAuthSuccessAsync, OpenRegisterWindow);
        var window = new LoginWindow
        {
            DataContext = vm
        };

        SwitchWindow(window);
    }

    private void OpenRegisterWindow()
    {
        if (_authSessionService is null)
            return;

        var vm = new RegisterViewModel(_authSessionService, HandleAuthSuccessAsync, OpenLoginWindow);
        var window = new RegisterWindow
        {
            DataContext = vm
        };

        SwitchWindow(window);
    }

    private void OpenMainWindow(AuthResponseDto authData)
    {
        if (_authSessionService is null)
            return;

        var vm = new MainWindowViewModel(_authSessionService, authData, OnLogoutAsync);
        var window = new MainWindow
        {
            DataContext = vm
        };

        SwitchWindow(window);
    }

    private async Task OnLogoutAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(OpenLoginWindow);
    }

    private void SwitchWindow(Window nextWindow)
    {
        if (_desktop is null)
            return;

        var previousWindow = _currentWindow;
        _currentWindow = nextWindow;

        _desktop.MainWindow = nextWindow;
        nextWindow.Show();
        previousWindow?.Close();
    }

    private static SessionSnapshot MapSession(AuthResponseDto authData)
    {
        return new SessionSnapshot
        {
            AccessToken = authData.Token,
            RefreshToken = authData.RefreshToken,
            UserId = authData.UserId,
            RoleTitle = authData.RoleTitle,
            AccessTokenExpiresAt = authData.ExpiresAt,
            RefreshTokenExpiresAt = authData.RefreshTokenExpiresAt
        };
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
