using API.DB;
using API.Models.DTO;
using API.Services.Token;
using API.Services.UserContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/logout")]
public class LogoutController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IUserContextService _userContextService;
    private readonly IAuthTokenService _authTokenService;

    public LogoutController(YarifyDbContext db, IUserContextService userContextService, IAuthTokenService authTokenService)
    {
        _db = db;
        _userContextService = userContextService;
        _authTokenService = authTokenService;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult> Logout(RefreshTokenRequestDto request)
    {
        var tokenHash = _authTokenService.HashToken(request.RefreshToken);
        var currentToken = await _db.Refreshtokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (currentToken is not null && currentToken.RevokedAt is null)
        {
            currentToken.RevokedAt = DateTime.UtcNow;
            currentToken.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Сессия завершена." });
    }

    [HttpPost("all")]
    [Authorize]
    public async Task<ActionResult> LogoutAll()
    {
        var userId = _userContextService.GetRequiredUserId(User);
        var activeTokens = await _db.Refreshtokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Все сессии завершены." });
    }
}

