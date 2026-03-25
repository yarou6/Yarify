using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using API.Services.Password;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/users-panel")]
public class UsersPanelController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IPasswordValidationService _passwordValidationService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IUserContextService _userContextService;

    public UsersPanelController(YarifyDbContext db, IPasswordValidationService passwordValidationService, IPasswordHasherService passwordHasherService, IUserContextService userContextService)
    {
        _db = db;
        _passwordValidationService = passwordValidationService;
        _passwordHasherService = passwordHasherService;
        _userContextService = userContextService;
    }

    [HttpPatch("role")]
    [Authorize(Roles = "User")]
    public async Task<ActionResult> UsersPanelRole(UsersPanelRoleRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.ArtistName))
            return BadRequest("ArtistName обязателен.");

        var userId = _userContextService.GetRequiredUserId(User);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return NotFound("Пользователь не найден.");

        var artistRole = await _db.Roles.FirstOrDefaultAsync(r => r.Title == "Artist");
        if (artistRole is null)
            return StatusCode(500, "Роль 'Artist' не найдена.");

        user.RoleId = artistRole.Id;
        user.ArtistName = request.ArtistName;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPatch("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequestDto request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
            return BadRequest("Новый пароль и подтверждение не совпадают.");

        if (request.CurrentPassword == request.NewPassword)
            return BadRequest("Новый пароль должен отличаться от текущего.");

        var passwordError = _passwordValidationService.ValidatePasswordPolicy(request.NewPassword);
        if (passwordError is not null)
            return BadRequest(passwordError);

        var userId = _userContextService.GetRequiredUserId(User);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return NotFound("Пользователь не найден.");

        if (!_passwordHasherService.Verify(request.CurrentPassword, user.PasswordHash))
            return Unauthorized("Текущий пароль указан неверно.");

        user.PasswordHash = _passwordHasherService.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        var activeTokens = await _db.Refreshtokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        await _db.SaveChangesAsync();

        return Ok(new { message = "Пароль успешно изменён." });
    }
}


