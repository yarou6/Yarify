namespace MVVM.Models.Auth;

public sealed class LoginRequestDto
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
