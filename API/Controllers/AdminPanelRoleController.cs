using API.DB;
using API.Models.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/admin-panel-role")]
public class AdminPanelRoleController : ControllerBase
{
    private readonly YarifyDbContext _db;

    public AdminPanelRoleController(YarifyDbContext db)
    {
        _db = db;
    }

    [HttpPatch("users/{userId:int}")]
    [Authorize(Roles = "Admin")]
    // Выполняет внутреннюю логику метода.
    public async Task<ActionResult> AdminPanelRole(int userId, AdminPanelRoleRequestDto request)
    {
        var targetRole = await _db.Roles.FirstOrDefaultAsync(r => r.Title == request.RoleTitle);
        if (targetRole is null)
            return BadRequest("Неизвестная роль. Допустимо: User, Artist, Admin.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return NotFound("Пользователь не найден.");

        user.RoleId = targetRole.Id;

        if (request.RoleTitle == "Artist")
        {
            if (string.IsNullOrWhiteSpace(request.ArtistName))
                return BadRequest("ArtistName обязателен для роли Artist.");

            user.ArtistName = request.ArtistName;
        }
        else
        {
            user.ArtistName = null;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }
}

