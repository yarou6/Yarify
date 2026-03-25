namespace MVVM.Models.Auth;

public sealed class SessionSnapshot
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public int UserId { get; set; }
    public string RoleTitle { get; set; } = string.Empty;
}
