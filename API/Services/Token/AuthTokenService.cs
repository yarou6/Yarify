using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using API.DB;
using API.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace API.Services.Token;

public sealed class AuthTokenService : IAuthTokenService
{
    private readonly YarifyDbContext _db;
    private readonly IConfiguration _config;

    public AuthTokenService(YarifyDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // Создает или добавляет новый элемент.
    public async Task<AuthResponseDto> CreateAccessTokenResponseAsync(User user)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == user.RoleId);
        var roleTitle = role?.Title ?? "User";

        var issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("Не задан Jwt:Issuer.");
        var audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("Не задан Jwt:Audience.");
        var key = _config["Jwt:Key"] ?? throw new InvalidOperationException("Не задан Jwt:Key.");
        var expiresInMinutes = int.Parse(_config["Jwt:ExpiresInMinutes"] ?? "120");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Login),
            new Claim(ClaimTypes.Role, roleTitle),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new AuthResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = token.ValidTo,
            UserId = user.Id,
            RoleTitle = roleTitle,
            RefreshToken = string.Empty,
            RefreshTokenExpiresAt = DateTime.MinValue
        };
    }

    public (string rawToken, Refreshtoken entity) CreateRefreshTokenEntity(int userId, HttpRequest request, int? refreshLifetimeDaysOverride = null)
    {
        var refreshTokenRaw = GenerateRefreshToken();
        var refreshLifetimeDays = refreshLifetimeDaysOverride ?? int.Parse(_config["Jwt:RefreshExpiresInDays"] ?? "30");

        var entity = new Refreshtoken
        {
            UserId = userId,
            TokenHash = HashToken(refreshTokenRaw),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshLifetimeDays),
            CreatedByIp = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            UserAgent = request.Headers.UserAgent.ToString()
        };

        return (refreshTokenRaw, entity);
    }

    // Выполняет внутреннюю логику метода.
    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    // Создает или добавляет новый элемент.
    public string CreatePasswordResetToken(User user)
    {
        var issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("Не задан Jwt:Issuer.");
        var audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("Не задан Jwt:Audience.");
        var key = _config["Jwt:PasswordResetKey"] ?? _config["Jwt:Key"] ?? throw new InvalidOperationException("Не задан Jwt:Key.");
        var expiresInMinutes = int.Parse(_config["Jwt:ResetExpiresInMinutes"] ?? "15");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("purpose", "password_reset"),
            new Claim("pwd_hash_stamp", GetPasswordHashStamp(user.PasswordHash)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Проверяет условие и возвращает результат проверки.
    public ClaimsPrincipal ValidatePasswordResetToken(string token)
    {
        var key = _config["Jwt:PasswordResetKey"] ?? _config["Jwt:Key"] ?? throw new InvalidOperationException("Не задан Jwt:Key.");
        var issuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("Не задан Jwt:Issuer.");
        var audience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("Не задан Jwt:Audience.");

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.Zero
        }, out _);

        var purpose = principal.FindFirstValue("purpose");
        if (!string.Equals(purpose, "password_reset", StringComparison.Ordinal))
            throw new SecurityTokenException("Wrong token purpose.");

        return principal;
    }

    // Готовит и возвращает нужные данные.
    public string GetPasswordHashStamp(string passwordHash)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash));
        return Convert.ToHexString(bytes);
    }

    // Выполняет внутреннюю логику метода.
    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}

