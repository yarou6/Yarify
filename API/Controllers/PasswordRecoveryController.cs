using System.Security.Claims;
using API.DB;
using API.Models.DTO;
using API.Services.Token;
using API.Services.Password;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/password-recovery")]
public class PasswordRecoveryController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IPasswordValidationService _passwordValidationService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IAuthTokenService _authTokenService;

    public PasswordRecoveryController(YarifyDbContext db, IPasswordValidationService passwordValidationService, IPasswordHasherService passwordHasherService, IAuthTokenService authTokenService)
    {
        _db = db;
        _passwordValidationService = passwordValidationService;
        _passwordHasherService = passwordHasherService;
        _authTokenService = authTokenService;
    }

    [HttpPost("forgot")]
    [AllowAnonymous]
    // Выполняет внутреннюю логику метода.
    public async Task<ActionResult<ForgotPasswordResponseDto>> ForgotPassword(ForgotPasswordRequestDto request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == request.LoginOrEmail || u.Email == request.LoginOrEmail);

        if (user is null || (user.IsActive.HasValue && !user.IsActive.Value))
        {
            return Ok(new ForgotPasswordResponseDto
            {
                Message = "Если пользователь существует, инструкция по сбросу пароля отправлена.",
                ResetToken = null
            });
        }

        var resetToken = _authTokenService.CreatePasswordResetToken(user);

        return Ok(new ForgotPasswordResponseDto
        {
            Message = "Токен для сброса пароля сгенерирован. В продакшене его нужно отправлять по email.",
            ResetToken = resetToken
        });
    }

    [HttpPost("reset")]
    [AllowAnonymous]
    // Выполняет внутреннюю логику метода.
    public async Task<ActionResult> ResetPassword(ResetPasswordRequestDto request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
            return BadRequest("Новый пароль и подтверждение не совпадают.");

        var passwordError = _passwordValidationService.ValidatePasswordPolicy(request.NewPassword);
        if (passwordError is not null)
            return BadRequest(passwordError);

        ClaimsPrincipal principal;
        try
        {
            principal = _authTokenService.ValidatePasswordResetToken(request.ResetToken);
        }
        catch
        {
            return Unauthorized("Недействительный или просроченный токен сброса.");
        }

        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var passwordHashStampClaim = principal.FindFirstValue("pwd_hash_stamp");

        if (!int.TryParse(userIdClaim, out var userId) || string.IsNullOrWhiteSpace(passwordHashStampClaim))
            return Unauthorized("Недействительный токен сброса.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return NotFound("Пользователь не найден.");

        if (user.IsActive.HasValue && !user.IsActive.Value)
            return Unauthorized("Пользователь неактивен.");

        if (!string.Equals(passwordHashStampClaim, _authTokenService.GetPasswordHashStamp(user.PasswordHash), StringComparison.Ordinal))
            return Unauthorized("Токен сброса больше недействителен. Запросите новый.");

        if (_passwordHasherService.Verify(request.NewPassword, user.PasswordHash))
            return BadRequest("Новый пароль должен отличаться от текущего.");

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


