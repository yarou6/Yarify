using API.DB;
using API.Models.DTO;
using API.Services.Token;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/session")]
public class SessionController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IAuthTokenService _authTokenService;

    public SessionController(YarifyDbContext db, IAuthTokenService authTokenService)
    {
        _db = db;
        _authTokenService = authTokenService;
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshTokenRequestDto request)
    {
        var tokenHash = _authTokenService.HashToken(request.RefreshToken);
        var currentToken = await _db.Refreshtokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (currentToken is null)
            return Unauthorized("Недействительный refresh token.");

        if (currentToken.RevokedAt is not null)
            return Unauthorized("Refresh token уже отозван.");

        if (currentToken.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized("Refresh token просрочен.");

        if (currentToken.User.IsActive.HasValue && !currentToken.User.IsActive.Value)
            return Unauthorized("Пользователь неактивен.");

        var (newRefreshTokenRaw, newRefreshTokenEntity) = _authTokenService.CreateRefreshTokenEntity(currentToken.UserId, Request);
        currentToken.RevokedAt = DateTime.UtcNow;
        currentToken.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        currentToken.ReplacedByTokenHash = newRefreshTokenEntity.TokenHash;

        _db.Refreshtokens.Add(newRefreshTokenEntity);
        await _db.SaveChangesAsync();

        var access = await _authTokenService.CreateAccessTokenResponseAsync(currentToken.User);
        return Ok(new AuthResponseDto
        {
            Token = access.Token,
            ExpiresAt = access.ExpiresAt,
            UserId = access.UserId,
            RoleTitle = access.RoleTitle,
            RefreshToken = newRefreshTokenRaw,
            RefreshTokenExpiresAt = newRefreshTokenEntity.ExpiresAt
        });
    }
}

