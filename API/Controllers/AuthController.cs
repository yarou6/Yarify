using API.DB;
using API.Models.DTO;
using API.Services.Token;
using API.Services.Password;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IConfiguration _config;
    private readonly IPasswordValidationService _passwordValidationService;
    private readonly IPasswordHasherService _passwordHasherService;
    private readonly IAuthTokenService _authTokenService;

    public AuthController(YarifyDbContext db, IConfiguration config, IPasswordValidationService passwordValidationService, IPasswordHasherService passwordHasherService, IAuthTokenService authTokenService)
    {
        _db = db;
        _config = config;
        _passwordValidationService = passwordValidationService;
        _passwordHasherService = passwordHasherService;
        _authTokenService = authTokenService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterRequestDto request)
    {
        if (request.Password != request.ConfirmPassword)
            return BadRequest("Пароль и подтверждение пароля не совпадают.");

        var passwordError = _passwordValidationService.ValidatePasswordPolicy(request.Password);
        if (passwordError is not null)
            return BadRequest(passwordError);

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Title == "User");
        if (role is null)
            return StatusCode(500, "Роль 'User' не найдена.");

        if (await _db.Users.AnyAsync(u => u.Login == request.Login))
            return Conflict("Логин уже существует.");

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict("Email уже существует.");

        var now = DateTime.UtcNow;
        var user = new User
        {
            RoleId = role.Id,
            DisplayName = request.DisplayName,
            ArtistName = null,
            Login = request.Login,
            Email = request.Email,
            PasswordHash = _passwordHasherService.Hash(request.Password),
            Phone = null,
            AvatarPath = null,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            LastLogin = null
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var freePlan = await _db.Subscriptionplans.FirstOrDefaultAsync(p => p.Title == "Free");
        if (freePlan is not null)
        {
            var alreadyHas = await _db.Userplans.AnyAsync(up => up.UserId == user.Id);
            if (!alreadyHas)
            {
                _db.Userplans.Add(new Userplan
                {
                    UserId = user.Id,
                    PlanId = freePlan.Id,
                    Status = "Active",
                    IsActive = true,
                    IsAutoRenew = false,
                    StartedAt = now,
                    ExpiresAt = null,
                    NextRenewAt = null
                });
                await _db.SaveChangesAsync();
            }
        }

        var response = await CreateAuthResponseAsync(user, null);
        return Ok(response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginRequestDto request)
    {
        var user = await AuthenticateUserAsync(request.Login, request.Password);
        if (user is null)
            return Unauthorized("Неверный логин или пароль или пользователь неактивен.");

        var response = await CreateAuthResponseAsync(user, null);
        return Ok(response);
    }

    [HttpPost("login-remember-me")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> LoginRememberMe(RememberMeLoginRequestDto request)
    {
        var user = await AuthenticateUserAsync(request.Login, request.Password);
        if (user is null)
            return Unauthorized("Неверный логин или пароль или пользователь неактивен.");

        var rememberDays = int.Parse(_config["Jwt:RememberMeRefreshExpiresInDays"] ?? "90");
        var response = await CreateAuthResponseAsync(user, rememberDays);
        return Ok(response);
    }

    private async Task<User?> AuthenticateUserAsync(string login, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == login);
        if (user is null)
            return null;

        if (user.IsActive.HasValue && !user.IsActive.Value)
            return null;

        if (!_passwordHasherService.Verify(password, user.PasswordHash))
            return null;

        user.LastLogin = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return user;
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(User user, int? refreshLifetimeDaysOverride)
    {
        var access = await _authTokenService.CreateAccessTokenResponseAsync(user);
        var (refreshTokenRaw, refreshTokenEntity) = _authTokenService.CreateRefreshTokenEntity(user.Id, Request, refreshLifetimeDaysOverride);

        _db.Refreshtokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = access.Token,
            ExpiresAt = access.ExpiresAt,
            UserId = access.UserId,
            RoleTitle = access.RoleTitle,
            RefreshToken = refreshTokenRaw,
            RefreshTokenExpiresAt = refreshTokenEntity.ExpiresAt
        };
    }
}


