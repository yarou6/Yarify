using System.Security.Claims;
using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/library")]
[Authorize]
public class LibraryController : ControllerBase
{
    private static readonly HashSet<string> AllowedSourceTypes = new(StringComparer.OrdinalIgnoreCase) { "Local", "Online" };

    private readonly YarifyDbContext _db;
    private readonly IUserContextService _userContext;

    public LibraryController(YarifyDbContext db, IUserContextService userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    [HttpGet("my/albums")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<List<ManageAlbumDto>>> GetMyAlbums()
    {
        var userId = _userContext.GetRequiredUserId(User);

        var albums = await _db.Albums
            .AsNoTracking()
            .Where(a => a.ArtistUserId == userId)
            .OrderByDescending(a => a.ReleaseDate)
            .ThenBy(a => a.Title)
            .Select(a => new ManageAlbumDto
            {
                Id = a.Id,
                Title = a.Title,
                CoverPath = a.CoverPath,
                ReleaseDate = a.ReleaseDate,
                TracksCount = a.Songs.Count
            })
            .ToListAsync();

        return Ok(albums);
    }

    [HttpPost("my/albums")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<ManageAlbumDto>> CreateAlbum([FromBody] CreateAlbumRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var entity = new Album
        {
            ArtistUserId = userId,
            Title = request.Title.Trim(),
            CoverPath = request.CoverPath?.Trim(),
            ReleaseDate = request.ReleaseDate,
            CreatedAt = DateTime.UtcNow
        };

        _db.Albums.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new ManageAlbumDto
        {
            Id = entity.Id,
            Title = entity.Title,
            CoverPath = entity.CoverPath,
            ReleaseDate = entity.ReleaseDate,
            TracksCount = 0
        });
    }

    [HttpPut("my/albums/{albumId:int}")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<ManageAlbumDto>> UpdateAlbum(int albumId, [FromBody] UpdateAlbumRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var album = await _db.Albums
            .Include(a => a.Songs)
            .FirstOrDefaultAsync(a => a.Id == albumId && a.ArtistUserId == userId);

        if (album is null)
            return NotFound(new ApiErrorResponse { Message = "Альбом не найден." });

        album.Title = request.Title.Trim();
        album.CoverPath = request.CoverPath?.Trim();
        album.ReleaseDate = request.ReleaseDate;

        await _db.SaveChangesAsync();

        return Ok(new ManageAlbumDto
        {
            Id = album.Id,
            Title = album.Title,
            CoverPath = album.CoverPath,
            ReleaseDate = album.ReleaseDate,
            TracksCount = album.Songs.Count
        });
    }

    [HttpDelete("my/albums/{albumId:int}")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult> DeleteAlbum(int albumId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var album = await _db.Albums
            .FirstOrDefaultAsync(a => a.Id == albumId && a.ArtistUserId == userId);

        if (album is null)
            return NotFound(new ApiErrorResponse { Message = "Альбом не найден." });

        var songs = await _db.Songs.Where(s => s.AlbumId == albumId).ToListAsync();
        foreach (var song in songs)
            song.AlbumId = null;

        _db.Albums.Remove(album);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("my/songs")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<List<ManageSongDto>>> GetMySongs([FromQuery] bool includeInactive = false)
    {
        var userId = _userContext.GetRequiredUserId(User);

        if (!CanManageSongs(User))
            return Forbid();

        var query = _db.Songs
            .AsNoTracking()
            .Where(s => s.ArtistUserId == userId)
            .Include(s => s.Genres)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(s => s.IsActive == null || s.IsActive == true);

        var songs = await query
            .OrderBy(s => s.Title)
            .Select(s => new ManageSongDto
            {
                Id = s.Id,
                ArtistUserId = s.ArtistUserId,
                AlbumId = s.AlbumId,
                Title = s.Title,
                DurationSec = s.DurationSec,
                SourceType = s.SourceType,
                LocalPath = s.LocalPath,
                StreamUrl = s.StreamUrl,
                ExternalId = s.ExternalId,
                CoverPath = s.CoverPath,
                TrackNumber = s.TrackNumber,
                Explicit = s.Explicit,
                PlayCount = s.PlayCount,
                IsActive = s.IsActive ?? true,
                Genres = s.Genres
                    .OrderBy(g => g.Title)
                    .Select(g => new GenreItemDto { Id = g.Id, Title = g.Title })
                    .ToList()
            })
            .ToListAsync();

        return Ok(songs);
    }

    [HttpPost("my/songs")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<ManageSongDto>> CreateSong([FromBody] CreateSongRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        if (!CanManageSongs(User))
            return Forbid();

        var sourceType = NormalizeSourceType(request.SourceType);
        if (sourceType is null)
            return BadRequest(new ApiErrorResponse { Message = "SourceType должен быть Local или Online." });

        if (request.AlbumId.HasValue)
        {
            var albumOk = await _db.Albums.AnyAsync(a => a.Id == request.AlbumId && a.ArtistUserId == userId);
            if (!albumOk)
                return BadRequest(new ApiErrorResponse { Message = "Альбом не найден или не принадлежит вам." });
        }

        var genreIds = request.GenreIds.Distinct().ToList();
        var genres = genreIds.Count == 0
            ? new List<Genre>()
            : await _db.Genres.Where(g => genreIds.Contains(g.Id)).ToListAsync();

        if (genres.Count != genreIds.Count)
            return BadRequest(new ApiErrorResponse { Message = "Некоторые жанры не найдены." });

        var song = new Song
        {
            ArtistUserId = userId,
            AlbumId = request.AlbumId,
            Title = request.Title.Trim(),
            DurationSec = request.DurationSec,
            SourceType = sourceType,
            LocalPath = request.LocalPath?.Trim(),
            StreamUrl = request.StreamUrl?.Trim(),
            ExternalId = request.ExternalId?.Trim(),
            CoverPath = request.CoverPath?.Trim(),
            TrackNumber = request.TrackNumber,
            Explicit = request.Explicit,
            PlayCount = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Genres = genres
        };

        _db.Songs.Add(song);
        await _db.SaveChangesAsync();

        return Ok(await BuildSongDtoAsync(song.Id));
    }

    [HttpPut("my/songs/{songId:int}")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<ManageSongDto>> UpdateSong(int songId, [FromBody] UpdateSongRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        if (!CanManageSongs(User))
            return Forbid();

        var sourceType = NormalizeSourceType(request.SourceType);
        if (sourceType is null)
            return BadRequest(new ApiErrorResponse { Message = "SourceType должен быть Local или Online." });

        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId && s.ArtistUserId == userId);
        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        if (request.AlbumId.HasValue)
        {
            var albumOk = await _db.Albums.AnyAsync(a => a.Id == request.AlbumId && a.ArtistUserId == userId);
            if (!albumOk)
                return BadRequest(new ApiErrorResponse { Message = "Альбом не найден или не принадлежит вам." });
        }

        song.AlbumId = request.AlbumId;
        song.Title = request.Title.Trim();
        song.DurationSec = request.DurationSec;
        song.SourceType = sourceType;
        song.LocalPath = request.LocalPath?.Trim();
        song.StreamUrl = request.StreamUrl?.Trim();
        song.ExternalId = request.ExternalId?.Trim();
        song.CoverPath = request.CoverPath?.Trim();
        song.TrackNumber = request.TrackNumber;
        song.Explicit = request.Explicit;
        song.IsActive = request.IsActive;
        song.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(await BuildSongDtoAsync(song.Id));
    }

    [HttpDelete("my/songs/{songId:int}")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult> DeleteSong(int songId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        if (!CanManageSongs(User))
            return Forbid();

        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId && s.ArtistUserId == userId);
        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        song.IsActive = false;
        song.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPut("my/songs/{songId:int}/genres")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<ManageSongDto>> UpdateSongGenres(int songId, [FromBody] UpdateSongGenresRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        if (!CanManageSongs(User))
            return Forbid();

        var song = await _db.Songs
            .Include(s => s.Genres)
            .FirstOrDefaultAsync(s => s.Id == songId && s.ArtistUserId == userId);

        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        var genreIds = request.GenreIds.Distinct().ToList();
        var genres = genreIds.Count == 0
            ? new List<Genre>()
            : await _db.Genres.Where(g => genreIds.Contains(g.Id)).ToListAsync();

        if (genres.Count != genreIds.Count)
            return BadRequest(new ApiErrorResponse { Message = "Некоторые жанры не найдены." });

        song.Genres.Clear();
        foreach (var genre in genres)
            song.Genres.Add(genre);

        song.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(await BuildSongDtoAsync(songId));
    }

    [HttpGet("genres")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<List<GenreItemDto>>> GetGenres()
    {
        var genres = await _db.Genres
            .AsNoTracking()
            .OrderBy(g => g.Title)
            .Select(g => new GenreItemDto
            {
                Id = g.Id,
                Title = g.Title
            })
            .ToListAsync();

        return Ok(genres);
    }

    [HttpPost("genres")]
    [Authorize(Roles = "Artist,Admin")]
    public async Task<ActionResult<GenreItemDto>> CreateGenre([FromBody] GenreItemDto request)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new ApiErrorResponse { Message = "Название жанра обязательно." });

        var exists = await _db.Genres.AnyAsync(g => g.Title == title);
        if (exists)
            return Conflict(new ApiErrorResponse { Message = "Жанр уже существует." });

        var genre = new Genre { Title = title };
        _db.Genres.Add(genre);
        await _db.SaveChangesAsync();

        return Ok(new GenreItemDto { Id = genre.Id, Title = genre.Title });
    }

    [HttpPost("my/songs/{songId:int}/upload-audio")]
    [Authorize(Roles = "Artist,Admin")]
    [RequestSizeLimit(200_000_000)]
    public async Task<ActionResult<MediaUploadResponseDto>> UploadSongAudio(int songId, IFormFile file)
    {
        var userId = _userContext.GetRequiredUserId(User);

        if (!CanManageSongs(User))
            return Forbid();

        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId && s.ArtistUserId == userId);
        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        if (file is null || file.Length == 0)
            return BadRequest(new ApiErrorResponse { Message = "Файл не передан." });

        var uploaded = await SaveFormFileAsync(file, "audio", "song", songId);

        song.LocalPath = uploaded.RelativePath;
        song.SourceType = "Local";
        song.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(uploaded);
    }

    [HttpPost("my/songs/{songId:int}/upload-cover")]
    [Authorize(Roles = "Artist,Admin")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<MediaUploadResponseDto>> UploadSongCover(int songId, IFormFile file)
    {
        var userId = _userContext.GetRequiredUserId(User);

        if (!CanManageSongs(User))
            return Forbid();

        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId && s.ArtistUserId == userId);
        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        if (file is null || file.Length == 0)
            return BadRequest(new ApiErrorResponse { Message = "Файл не передан." });

        var uploaded = await SaveFormFileAsync(file, "covers", "song", songId);

        song.CoverPath = uploaded.RelativePath;
        song.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(uploaded);
    }

    [HttpPost("my/albums/{albumId:int}/upload-cover")]
    [Authorize(Roles = "Artist,Admin")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<MediaUploadResponseDto>> UploadAlbumCover(int albumId, IFormFile file)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var album = await _db.Albums.FirstOrDefaultAsync(a => a.Id == albumId && a.ArtistUserId == userId);
        if (album is null)
            return NotFound(new ApiErrorResponse { Message = "Альбом не найден." });

        if (file is null || file.Length == 0)
            return BadRequest(new ApiErrorResponse { Message = "Файл не передан." });

        var uploaded = await SaveFormFileAsync(file, "covers", "album", albumId);

        album.CoverPath = uploaded.RelativePath;
        await _db.SaveChangesAsync();

        return Ok(uploaded);
    }

    private static bool CanManageSongs(ClaimsPrincipal user)
    {
        return user.IsInRole("Artist") || user.IsInRole("Admin");
    }

    private static string? NormalizeSourceType(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return "Local";

        if (!AllowedSourceTypes.Contains(sourceType))
            return null;

        return string.Equals(sourceType, "Online", StringComparison.OrdinalIgnoreCase) ? "Online" : "Local";
    }

    private async Task<ManageSongDto> BuildSongDtoAsync(int songId)
    {
        var song = await _db.Songs
            .AsNoTracking()
            .Include(s => s.Genres)
            .FirstAsync(s => s.Id == songId);

        return new ManageSongDto
        {
            Id = song.Id,
            ArtistUserId = song.ArtistUserId,
            AlbumId = song.AlbumId,
            Title = song.Title,
            DurationSec = song.DurationSec,
            SourceType = song.SourceType,
            LocalPath = song.LocalPath,
            StreamUrl = song.StreamUrl,
            ExternalId = song.ExternalId,
            CoverPath = song.CoverPath,
            TrackNumber = song.TrackNumber,
            Explicit = song.Explicit,
            PlayCount = song.PlayCount,
            IsActive = song.IsActive ?? true,
            Genres = song.Genres
                .OrderBy(g => g.Title)
                .Select(g => new GenreItemDto { Id = g.Id, Title = g.Title })
                .ToList()
        };
    }

    private static async Task<MediaUploadResponseDto> SaveFormFileAsync(IFormFile file, string bucket, string entityType, int entityId)
    {
        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{entityType}_{entityId}_{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine("uploads", bucket, fileName).Replace('\\', '/');
        var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var stream = System.IO.File.Create(absolutePath))
            await file.CopyToAsync(stream);

        return new MediaUploadResponseDto
        {
            RelativePath = "/" + relativePath,
            Length = file.Length,
            ContentType = file.ContentType ?? "application/octet-stream"
        };
    }
}
