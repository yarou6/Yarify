using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/lyrics")]
[Authorize]
public class LyricsController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IUserContextService _userContext;

    public LyricsController(YarifyDbContext db, IUserContextService userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    [HttpGet("song/{songId:int}")]
    // Готовит и возвращает нужные данные.
    public async Task<ActionResult<List<LyricItemDto>>> GetSongLyrics(int songId)
    {
        var exists = await _db.Songs.AnyAsync(s => s.Id == songId && (s.IsActive == null || s.IsActive == true));
        if (!exists)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        var lyrics = await _db.Songlyrics
            .AsNoTracking()
            .Where(l => l.SongId == songId)
            .OrderBy(l => l.LanguageCode)
            .Select(l => new LyricItemDto
            {
                SongId = l.SongId,
                LanguageCode = l.LanguageCode,
                LyricsText = l.LyricsText,
                SourceType = l.SourceType,
                UpdatedAt = l.UpdatedAt
            })
            .ToListAsync();

        return Ok(lyrics);
    }

    [HttpGet("song/{songId:int}/{languageCode}")]
    // Готовит и возвращает нужные данные.
    public async Task<ActionResult<LyricItemDto>> GetSongLyric(int songId, string languageCode)
    {
        var lang = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lang))
            return BadRequest(new ApiErrorResponse { Message = "LanguageCode обязателен." });

        var lyric = await _db.Songlyrics
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.SongId == songId && l.LanguageCode == lang);

        if (lyric is null)
            return NotFound(new ApiErrorResponse { Message = "Лирика не найдена." });

        return Ok(new LyricItemDto
        {
            SongId = lyric.SongId,
            LanguageCode = lyric.LanguageCode,
            LyricsText = lyric.LyricsText,
            SourceType = lyric.SourceType,
            UpdatedAt = lyric.UpdatedAt
        });
    }

    [HttpPut("song/{songId:int}")]
    [Authorize(Roles = "Artist,Admin")]
    // Выполняет внутреннюю логику метода.
    public async Task<ActionResult<LyricItemDto>> UpsertSongLyric(int songId, [FromBody] UpsertLyricRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var isAdmin = User.IsInRole("Admin");

        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId);
        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        if (!isAdmin && song.ArtistUserId != userId)
            return Forbid();

        var lang = request.LanguageCode.Trim().ToLowerInvariant();
        var sourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Manual" : request.SourceType.Trim();
        if (sourceType is not ("Manual" or "Imported"))
            return BadRequest(new ApiErrorResponse { Message = "SourceType должен быть Manual или Imported." });

        var lyric = await _db.Songlyrics.FirstOrDefaultAsync(l => l.SongId == songId && l.LanguageCode == lang);

        if (lyric is null)
        {
            lyric = new Songlyric
            {
                SongId = songId,
                LanguageCode = lang,
                LyricsText = request.LyricsText,
                SourceType = sourceType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Songlyrics.Add(lyric);
        }
        else
        {
            lyric.LyricsText = request.LyricsText;
            lyric.SourceType = sourceType;
            lyric.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return Ok(new LyricItemDto
        {
            SongId = lyric.SongId,
            LanguageCode = lyric.LanguageCode,
            LyricsText = lyric.LyricsText,
            SourceType = lyric.SourceType,
            UpdatedAt = lyric.UpdatedAt
        });
    }

    [HttpDelete("song/{songId:int}/{languageCode}")]
    [Authorize(Roles = "Artist,Admin")]
    // Удаляет элемент из текущего контекста.
    public async Task<ActionResult> DeleteSongLyric(int songId, string languageCode)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var isAdmin = User.IsInRole("Admin");

        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId);
        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        if (!isAdmin && song.ArtistUserId != userId)
            return Forbid();

        var lang = (languageCode ?? string.Empty).Trim().ToLowerInvariant();
        var lyric = await _db.Songlyrics.FirstOrDefaultAsync(l => l.SongId == songId && l.LanguageCode == lang);
        if (lyric is null)
            return Ok();

        _db.Songlyrics.Remove(lyric);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
