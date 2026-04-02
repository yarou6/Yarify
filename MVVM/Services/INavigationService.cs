using MVVM.Models.Auth;

namespace MVVM.Services;

public interface INavigationService
{
    void OpenLogin();
    void OpenRegister();
    void OpenMain(AuthResponseDto authData);
}
