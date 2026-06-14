using API.DB;
using API.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly YarifyDbContext _db;

    public AdminUsersController(YarifyDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    // Готовит и возвращает нужные данные.
    public async Task<ActionResult<List<AdminUserItemDto>>> GetUsers([FromQuery] string? query, [FromQuery] int take = 100)
    {
        var normalizedTake = Math.Clamp(take, 1, 500);
        var q = query?.Trim();

        var usersQuery = _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            usersQuery = usersQuery.Where(u =>
                EF.Functions.Like(u.DisplayName, $"%{q}%") ||
                EF.Functions.Like(u.Login, $"%{q}%") ||
                EF.Functions.Like(u.Email, $"%{q}%"));
        }

        var users = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .Take(normalizedTake)
            .Select(u => new AdminUserItemDto
            {
                UserId = u.Id,
                DisplayName = u.DisplayName,
                Login = u.Login,
                Email = u.Email,
                ArtistName = u.ArtistName,
                IsActive = u.IsActive ?? true,
                RoleTitle = u.Role.Title,
                CreatedAt = u.CreatedAt,
                LastLogin = u.LastLogin
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{userId:int}")]
    // Готовит и возвращает нужные данные.
    public async Task<ActionResult<AdminUserItemDto>> GetUser(int userId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound(new ApiErrorResponse { Message = "Пользователь не найден." });

        return Ok(new AdminUserItemDto
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Login = user.Login,
            Email = user.Email,
            ArtistName = user.ArtistName,
            IsActive = user.IsActive ?? true,
            RoleTitle = user.Role.Title,
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin
        });
    }

    [HttpPatch("{userId:int}/active")]
    // Обновляет состояние и приводит данные к нужному виду.
    public async Task<ActionResult<AdminUserItemDto>> SetUserActive(int userId, [FromBody] SetUserActiveRequestDto request)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound(new ApiErrorResponse { Message = "Пользователь не найден." });

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        if (!request.IsActive)
        {
            var activeTokens = await _db.Refreshtokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new AdminUserItemDto
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Login = user.Login,
            Email = user.Email,
            ArtistName = user.ArtistName,
            IsActive = user.IsActive ?? true,
            RoleTitle = user.Role.Title,
            CreatedAt = user.CreatedAt,
            LastLogin = user.LastLogin
        });
    }
}
