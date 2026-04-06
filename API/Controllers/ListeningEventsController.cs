using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/listening-events")]
[Authorize]
public class ListeningEventsController : ControllerBase
{
    private static readonly HashSet<string> AllowedSourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Playlist", "Album", "Search", "Direct", "Queue"
    };

    private readonly YarifyDbContext _db;
    private readonly IUserContextService _userContext;

    public ListeningEventsController(YarifyDbContext db, IUserContextService userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    [HttpPost("start")]
    public async Task<ActionResult<ListeningEventCreatedDto>> Start([FromBody] StartListeningEventRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var songExists = await _db.Songs.AnyAsync(s => s.Id == request.SongId && (s.IsActive == null || s.IsActive == true));
        if (!songExists)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        var sourceType = NormalizeSourceType(request.SourceType);
        if (sourceType is null)
            return BadRequest(new ApiErrorResponse { Message = "Некорректный SourceType." });

        var startedAt = request.StartedAt ?? DateTime.UtcNow;

        var entity = new Listeningevent
        {
            UserId = userId,
            SongId = request.SongId,
            StartedAt = startedAt,
            EndedAt = null,
            PlayedMs = 0,
            Completed = false,
            SourceType = sourceType,
            SourceId = request.SourceId
        };

        _db.Listeningevents.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new ListeningEventCreatedDto
        {
            EventId = entity.Id,
            StartedAt = entity.StartedAt
        });
    }

    [HttpPatch("{eventId:long}/progress")]
    public async Task<ActionResult> Progress(long eventId, [FromBody] ListeningEventProgressRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var entity = await _db.Listeningevents.FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);
        if (entity is null)
            return NotFound(new ApiErrorResponse { Message = "Событие прослушивания не найдено." });

        entity.PlayedMs = Math.Max(entity.PlayedMs, request.PlayedMs);
        if (request.EndedAt.HasValue)
            entity.EndedAt = request.EndedAt;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{eventId:long}/complete")]
    public async Task<ActionResult> Complete(long eventId, [FromBody] CompleteListeningEventRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var entity = await _db.Listeningevents.FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == userId);
        if (entity is null)
            return NotFound(new ApiErrorResponse { Message = "Событие прослушивания не найдено." });

        entity.PlayedMs = Math.Max(entity.PlayedMs, request.PlayedMs);
        var songDurationMs = await _db.Songs
            .AsNoTracking()
            .Where(s => s.Id == entity.SongId)
            .Select(s => s.DurationSec * 1000)
            .FirstOrDefaultAsync();
        var isFullyPlayed = songDurationMs > 0 && entity.PlayedMs >= songDurationMs;

        entity.Completed = request.Completed || isFullyPlayed;
        entity.EndedAt = request.EndedAt ?? DateTime.UtcNow;

        var shouldCountPlay = isFullyPlayed;
        if (shouldCountPlay)
            await CountPlayAsync(entity, userId);

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("rebuild-daily")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> RebuildDaily([FromQuery] int days = 30)
    {
        var normalizedDays = Math.Clamp(days, 1, 365);
        var fromUtc = DateTime.UtcNow.Date.AddDays(-normalizedDays);

        var events = await _db.Listeningevents
            .AsNoTracking()
            .Where(e => e.EndedAt.HasValue && e.EndedAt >= fromUtc && e.PlayedMs >= (e.Song.DurationSec * 1000))
            .ToListAsync();

        var grouped = events
            .GroupBy(e => new { Date = DateOnly.FromDateTime(e.EndedAt!.Value), e.SongId })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.SongId,
                PlaysCount = g.Count(),
                UniqueListeners = g.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().Count()
            })
            .ToList();

        var dates = grouped.Select(x => x.Date).Distinct().ToList();
        var existing = await _db.Songstatsdailies
            .Where(s => dates.Contains(s.StartDate))
            .ToListAsync();

        _db.Songstatsdailies.RemoveRange(existing);

        foreach (var row in grouped)
        {
            _db.Songstatsdailies.Add(new Songstatsdaily
            {
                StartDate = row.Date,
                SongId = row.SongId,
                PlaysCount = row.PlaysCount,
                UniqueListeners = row.UniqueListeners
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { rebuiltDays = normalizedDays, rows = grouped.Count });
    }

    private async Task CountPlayAsync(Listeningevent entity, int userId)
    {
        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == entity.SongId);
        if (song is not null)
            song.PlayCount += 1;

        var day = DateOnly.FromDateTime((entity.EndedAt ?? DateTime.UtcNow).Date);

        var stat = await _db.Songstatsdailies
            .FirstOrDefaultAsync(s => s.StartDate == day && s.SongId == entity.SongId);

        if (stat is null)
        {
            stat = new Songstatsdaily
            {
                StartDate = day,
                SongId = entity.SongId,
                PlaysCount = 0,
                UniqueListeners = 0
            };
            _db.Songstatsdailies.Add(stat);
        }

        stat.PlaysCount += 1;

        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        var hadEventsThisDay = await _db.Listeningevents
            .AsNoTracking()
            .AnyAsync(e =>
                e.Id != entity.Id &&
                e.UserId == userId &&
                e.SongId == entity.SongId &&
                e.PlayedMs >= (e.Song.DurationSec * 1000) &&
                (
                    (e.EndedAt.HasValue && e.EndedAt.Value >= dayStart && e.EndedAt.Value < dayEnd) ||
                    (!e.EndedAt.HasValue && e.StartedAt >= dayStart && e.StartedAt < dayEnd)
                ));

        if (!hadEventsThisDay)
            stat.UniqueListeners += 1;
    }

    private static string? NormalizeSourceType(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return null;

        return AllowedSourceTypes.Contains(sourceType)
            ? AllowedSourceTypes.First(s => s.Equals(sourceType, StringComparison.OrdinalIgnoreCase))
            : null;
    }
}
