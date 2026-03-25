using System.Security.Claims;
using API.DB;
using API.Models.DTO;

namespace API.Services.Token;

public interface IAuthTokenService
{
    Task<AuthResponseDto> CreateAccessTokenResponseAsync(User user);
    (string rawToken, Refreshtoken entity) CreateRefreshTokenEntity(int userId, HttpRequest request, int? refreshLifetimeDaysOverride = null);
    string HashToken(string token);

    string CreatePasswordResetToken(User user);
    ClaimsPrincipal ValidatePasswordResetToken(string token);
    string GetPasswordHashStamp(string passwordHash);
}

