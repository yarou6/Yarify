using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IUserContextService _userContext;

    public ProfileController(YarifyDbContext db, IUserContextService userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    [HttpGet("me")]
    public async Task<ActionResult<ProfileMeDto>> GetMe()
    {
        var userId = _userContext.GetRequiredUserId(User);

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound(new ApiErrorResponse { Message = "Пользователь не найден." });

        return Ok(new ProfileMeDto
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            ArtistName = user.ArtistName,
            Login = user.Login,
            Email = user.Email,
            Phone = user.Phone,
            AvatarPath = user.AvatarPath,
            IsActive = user.IsActive ?? true,
            RoleTitle = user.Role.Title
        });
    }

    [HttpPut("me")]
    public async Task<ActionResult<ProfileMeDto>> UpdateMe([FromBody] UpdateProfileRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var user = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound(new ApiErrorResponse { Message = "Пользователь не найден." });

        user.DisplayName = request.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var nextEmail = request.Email.Trim();
            var usedByOther = await _db.Users.AnyAsync(u =>
                u.Id != userId &&
                u.Email.ToLower() == nextEmail.ToLower());

            if (usedByOther)
                return BadRequest(new ApiErrorResponse { Message = "Эта почта уже занята." });

            user.Email = nextEmail;
        }

        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

        if (user.Role.Title is "Artist" or "Admin")
            user.ArtistName = string.IsNullOrWhiteSpace(request.ArtistName) ? null : request.ArtistName.Trim();

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new ProfileMeDto
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            ArtistName = user.ArtistName,
            Login = user.Login,
            Email = user.Email,
            Phone = user.Phone,
            AvatarPath = user.AvatarPath,
            IsActive = user.IsActive ?? true,
            RoleTitle = user.Role.Title
        });
    }

    [HttpPost("me/avatar")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<MediaUploadResponseDto>> UploadAvatar(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiErrorResponse { Message = "Файл не передан." });

        var userId = _userContext.GetRequiredUserId(User);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return NotFound(new ApiErrorResponse { Message = "Пользователь не найден." });

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"avatar_{userId}_{Guid.NewGuid():N}{ext}";
        var relativePath = Path.Combine("uploads", "avatars", fileName).Replace('\\', '/');
        var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var stream = System.IO.File.Create(absolutePath))
            await file.CopyToAsync(stream);

        user.AvatarPath = "/" + relativePath;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new MediaUploadResponseDto
        {
            RelativePath = user.AvatarPath,
            Length = file.Length,
            ContentType = file.ContentType ?? "application/octet-stream"
        });
    }
}
