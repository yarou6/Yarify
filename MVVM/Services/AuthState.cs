using MVVM.Models.Auth;

namespace MVVM.Services;

public sealed class AuthState
{
    private AuthResponseDto? _current;

    public AuthResponseDto? Current
    {
        get => _current;
        set
        {
            _current = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? Changed;
}
