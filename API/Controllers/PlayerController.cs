using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[ApiController]
[Route("api/player")]
[Authorize]
public class PlayerController : ControllerBase
{
    private readonly YarifyDbContext _db;
    private readonly IUserContextService _userContext;

    public PlayerController(YarifyDbContext db, IUserContextService userContext)
    {
        _db = db;
        _userContext = userContext;
    }

    [HttpGet("tracks")]
    public async Task<ActionResult<List<TrackListItemDto>>> GetTracks([FromQuery] string? query, [FromQuery] string? genre, [FromQuery] string? sort)
    {
        var tracksQuery = _db.Songs
            .AsNoTracking()
            .Where(s => s.IsActive == null || s.IsActive == true)
            .Include(s => s.ArtistUser)
            .Include(s => s.Genres)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            tracksQuery = tracksQuery.Where(s =>
                EF.Functions.Like(s.Title, $"%{q}%") ||
                EF.Functions.Like(s.ArtistUser.DisplayName, $"%{q}%") ||
                (s.ArtistUser.ArtistName != null && EF.Functions.Like(s.ArtistUser.ArtistName, $"%{q}%")));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            var g = genre.Trim();
            tracksQuery = tracksQuery.Where(s => s.Genres.Any(x => x.Title == g));
        }

        tracksQuery = (sort ?? string.Empty).ToLowerInvariant() switch
        {
            "artist" => tracksQuery.OrderBy(s => s.ArtistUser.DisplayName).ThenBy(s => s.Title),
            "duration" => tracksQuery.OrderByDescending(s => s.DurationSec).ThenBy(s => s.Title),
            _ => tracksQuery.OrderBy(s => s.Title)
        };

        var tracks = await tracksQuery
            .Take(500)
            .Select(ToTrackDtoExpr())
            .ToListAsync();

        return Ok(tracks);
    }

    [HttpGet("genres")]
    public async Task<ActionResult<List<GenreItemDto>>> GetGenres()
    {
        var genres = await _db.Genres
            .AsNoTracking()
            .OrderBy(g => g.Title)
            .Select(g => new GenreItemDto { Id = g.Id, Title = g.Title })
            .ToListAsync();

        return Ok(genres);
    }
    [HttpGet("home")]
    public async Task<ActionResult<PlayerHomeResponseDto>> GetHome()
    {
        var userId = _userContext.GetRequiredUserId(User);

        var recentSongIds = await _db.Listeningevents
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => e.SongId)
            .Distinct()
            .Take(20)
            .ToListAsync();

        var recentSort = recentSongIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        var recentlyPlayed = await _db.Songs
            .AsNoTracking()
            .Where(s => recentSongIds.Contains(s.Id))
            .Where(s => s.IsActive == null || s.IsActive == true)
            .Include(s => s.ArtistUser)
            .Select(ToTrackDtoExpr())
            .ToListAsync();

        recentlyPlayed = recentlyPlayed
            .OrderBy(t => recentSort.TryGetValue(t.Id, out var idx) ? idx : int.MaxValue)
            .ToList();

        var trendingTracks = await _db.Songs
            .AsNoTracking()
            .Where(s => s.IsActive == null || s.IsActive == true)
            .Include(s => s.ArtistUser)
            .OrderByDescending(s => s.PlayCount)
            .ThenByDescending(s => s.CreatedAt)
            .Take(20)
            .Select(ToTrackDtoExpr())
            .ToListAsync();

        var newReleases = await _db.Albums
            .AsNoTracking()
            .OrderByDescending(a => a.ReleaseDate)
            .ThenByDescending(a => a.CreatedAt)
            .Take(12)
            .Select(a => new AlbumListItemDto
            {
                Id = a.Id,
                Title = a.Title,
                CoverPath = a.CoverPath ?? a.Songs.Select(s => s.CoverPath).FirstOrDefault(),
                ReleaseDate = a.ReleaseDate,
                TracksCount = a.Songs.Count
            })
            .ToListAsync();

        var followingArtistIds = await _db.Follows
            .AsNoTracking()
            .Where(f => f.SubscriberUserId == userId && (f.IsActive == null || f.IsActive == true))
            .Select(f => f.ArtistUserId)
            .ToListAsync();

        var recommendedArtists = await _db.Users
            .AsNoTracking()
            .Where(u => (u.IsActive == null || u.IsActive == true) && u.Id != userId)
            .Where(u => u.Songs.Any(s => s.IsActive == null || s.IsActive == true))
            .OrderByDescending(u => u.Songs.Sum(s => (long?)s.PlayCount) ?? 0)
            .Take(12)
            .Select(u => new ArtistCardItemDto
            {
                ArtistUserId = u.Id,
                ArtistName = string.IsNullOrWhiteSpace(u.ArtistName) ? u.DisplayName : u.ArtistName!,
                AvatarPath = u.AvatarPath,
                FollowersCount = u.FollowArtistUsers.Count(f => f.IsActive == null || f.IsActive == true),
                IsFollowing = followingArtistIds.Contains(u.Id)
            })
            .ToListAsync();

        return Ok(new PlayerHomeResponseDto
        {
            RecentlyPlayed = recentlyPlayed,
            TrendingTracks = trendingTracks,
            NewReleases = newReleases,
            RecommendedArtists = recommendedArtists
        });
    }

    [HttpGet("history/recent")]
    public async Task<ActionResult<List<ListeningHistoryItemDto>>> GetRecentHistory([FromQuery] int take = 50)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var normalizedTake = Math.Clamp(take, 1, 200);

        var events = await _db.Listeningevents
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.StartedAt)
            .Take(normalizedTake)
            .Include(e => e.Song)
            .ThenInclude(s => s.ArtistUser)
            .Select(e => new ListeningHistoryItemDto
            {
                EventId = e.Id,
                StartedAt = e.StartedAt,
                EndedAt = e.EndedAt,
                PlayedMs = e.PlayedMs,
                Completed = e.Completed,
                SourceType = e.SourceType,
                SourceId = e.SourceId,
                Track = new TrackListItemDto
                {
                    Id = e.Song.Id,
                    Title = e.Song.Title,
                    Artist = e.Song.ArtistUser.ArtistName != null && e.Song.ArtistUser.ArtistName != "" ? e.Song.ArtistUser.ArtistName : e.Song.ArtistUser.DisplayName,
                    DurationSec = e.Song.DurationSec,
                    StreamUrl = e.Song.StreamUrl,
                    LocalPath = e.Song.LocalPath,
                    CoverPath = e.Song.CoverPath ?? (e.Song.Album != null ? e.Song.Album.CoverPath : null),
                    ArtistUserId = e.Song.ArtistUserId,
                    AlbumId = e.Song.AlbumId,
                    AlbumTitle = e.Song.Album != null ? e.Song.Album.Title : null
                }
            })
            .ToListAsync();

        return Ok(events);
    }

    [HttpGet("history/summary")]
    public async Task<ActionResult<ListeningSummaryDto>> GetHistorySummary([FromQuery] int days = 30)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var normalizedDays = Math.Clamp(days, 1, 365);
        var fromUtc = DateTime.UtcNow.AddDays(-normalizedDays);

        var query = _db.Listeningevents
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.StartedAt >= fromUtc);

        var playsCount = await query.CountAsync();
        var completedCount = await query.CountAsync(e => e.Completed);
        var totalPlayedMs = await query.SumAsync(e => (int?)e.PlayedMs) ?? 0;
        var uniqueTracks = await query.Select(e => e.SongId).Distinct().CountAsync();
        var uniqueArtists = await query
            .Include(e => e.Song)
            .Select(e => e.Song.ArtistUserId)
            .Distinct()
            .CountAsync();

        return Ok(new ListeningSummaryDto
        {
            Days = normalizedDays,
            PlaysCount = playsCount,
            CompletedCount = completedCount,
            TotalPlayedMs = totalPlayedMs,
            UniqueTracks = uniqueTracks,
            UniqueArtists = uniqueArtists
        });
    }

    [HttpGet("stats/top-tracks")]
    public async Task<ActionResult<List<TrackStatItemDto>>> GetTopTracks([FromQuery] int days = 30, [FromQuery] int take = 20)
    {
        var normalizedDays = Math.Clamp(days, 1, 365);
        var normalizedTake = Math.Clamp(take, 1, 100);
        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-normalizedDays));

        var daily = await _db.Songstatsdailies
            .AsNoTracking()
            .Where(s => s.StartDate >= fromDate)
            .GroupBy(s => s.SongId)
            .Select(g => new
            {
                SongId = g.Key,
                PlaysCount = g.Sum(x => x.PlaysCount),
                UniqueListeners = g.Sum(x => x.UniqueListeners)
            })
            .OrderByDescending(x => x.PlaysCount)
            .ThenByDescending(x => x.UniqueListeners)
            .Take(normalizedTake)
            .ToListAsync();

        var songIds = daily.Select(x => x.SongId).ToList();
        var tracks = await _db.Songs
            .AsNoTracking()
            .Where(s => songIds.Contains(s.Id))
            .Include(s => s.ArtistUser)
            .Select(ToTrackDtoExpr())
            .ToListAsync();

        var byId = tracks.ToDictionary(t => t.Id);
        var result = daily
            .Where(x => byId.ContainsKey(x.SongId))
            .Select(x => new TrackStatItemDto
            {
                Track = byId[x.SongId],
                PlaysCount = x.PlaysCount,
                UniqueListeners = x.UniqueListeners
            })
            .ToList();

        return Ok(result);
    }

    [HttpGet("public/playlists")]
    public async Task<ActionResult<List<PublicPlaylistItemDto>>> GetPublicPlaylists([FromQuery] string? query, [FromQuery] int take = 50)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        var q = query?.Trim();

        var playlists = _db.Playlists
            .AsNoTracking()
            .Where(p => p.IsPublic)
            .Include(p => p.OwnerUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            playlists = playlists.Where(p =>
                EF.Functions.Like(p.Title, $"%{q}%") ||
                (p.Description != null && EF.Functions.Like(p.Description, $"%{q}%")) ||
                EF.Functions.Like(p.OwnerUser.DisplayName, $"%{q}%"));
        }

        var data = await playlists
            .OrderByDescending(p => p.UpdatedAt)
            .Take(normalizedTake)
            .Select(p => new PublicPlaylistItemDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                CoverPath = p.CoverPath,
                TracksCount = p.Playlistsongs.Count,
                OwnerUserId = p.OwnerUserId,
                OwnerName = p.OwnerUser.DisplayName
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("public/playlists/{playlistId:int}")]
    public async Task<ActionResult<PublicPlaylistDetailsDto>> GetPublicPlaylist(int playlistId)
    {
        var playlist = await _db.Playlists
            .AsNoTracking()
            .Include(p => p.OwnerUser)
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.IsPublic);

        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Публичный плейлист не найден." });

        var tracks = await _db.Playlistsongs
            .AsNoTracking()
            .Where(ps => ps.PlaylistId == playlistId)
            .OrderBy(ps => ps.Position)
            .Include(ps => ps.Song)
            .ThenInclude(s => s.ArtistUser)
            .Select(ps => new PlaylistTrackItemDto
            {
                Position = ps.Position,
                Track = new TrackListItemDto
                {
                    Id = ps.Song.Id,
                    Title = ps.Song.Title,
                    Artist = ps.Song.ArtistUser.ArtistName != null && ps.Song.ArtistUser.ArtistName != "" ? ps.Song.ArtistUser.ArtistName : ps.Song.ArtistUser.DisplayName,
                    DurationSec = ps.Song.DurationSec,
                    StreamUrl = ps.Song.StreamUrl,
                    LocalPath = ps.Song.LocalPath,
                    CoverPath = ps.Song.CoverPath,
                    ArtistUserId = ps.Song.ArtistUserId,
                    AlbumId = ps.Song.AlbumId,
                    AlbumTitle = ps.Song.Album != null ? ps.Song.Album.Title : null
                }
            })
            .ToListAsync();

        return Ok(new PublicPlaylistDetailsDto
        {
            Id = playlist.Id,
            Title = playlist.Title,
            Description = playlist.Description,
            CoverPath = playlist.CoverPath,
            TracksCount = tracks.Count,
            OwnerUserId = playlist.OwnerUserId,
            OwnerName = playlist.OwnerUser.DisplayName,
            Tracks = tracks
        });
    }

    [HttpGet("public/artists")]
    public async Task<ActionResult<List<PublicArtistItemDto>>> GetPublicArtists([FromQuery] string? query, [FromQuery] int take = 50)
    {
        var normalizedTake = Math.Clamp(take, 1, 200);
        var q = query?.Trim();

        var artists = _db.Users
            .AsNoTracking()
            .Where(u => (u.IsActive == null || u.IsActive == true))
            .Where(u => u.Songs.Any(s => s.IsActive == null || s.IsActive == true))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            artists = artists.Where(u =>
                EF.Functions.Like(u.DisplayName, $"%{q}%") ||
                (u.ArtistName != null && EF.Functions.Like(u.ArtistName, $"%{q}%")));
        }

        var data = await artists
            .OrderByDescending(u => u.FollowArtistUsers.Count(f => f.IsActive == null || f.IsActive == true))
            .ThenBy(u => u.DisplayName)
            .Take(normalizedTake)
            .Select(u => new PublicArtistItemDto
            {
                ArtistUserId = u.Id,
                ArtistName = string.IsNullOrWhiteSpace(u.ArtistName) ? u.DisplayName : u.ArtistName!,
                AvatarPath = u.AvatarPath,
                FollowersCount = u.FollowArtistUsers.Count(f => f.IsActive == null || f.IsActive == true),
                TracksCount = u.Songs.Count(s => s.IsActive == null || s.IsActive == true)
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("public/artists/{artistUserId:int}")]
    public async Task<ActionResult<ArtistDetailsDto>> GetPublicArtist(int artistUserId)
    {
        var artist = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == artistUserId && (u.IsActive == null || u.IsActive == true));
        if (artist is null)
            return NotFound(new ApiErrorResponse { Message = "Артист не найден." });

        var topTracks = await _db.Songs
            .AsNoTracking()
            .Where(s => s.ArtistUserId == artistUserId && (s.IsActive == null || s.IsActive == true))
            .Include(s => s.ArtistUser)
            .OrderByDescending(s => s.PlayCount)
            .ThenBy(s => s.Title)
            .Take(20)
            .Select(ToTrackDtoExpr())
            .ToListAsync();

        var albums = await _db.Albums
            .AsNoTracking()
            .Where(a => a.ArtistUserId == artistUserId)
            .OrderByDescending(a => a.ReleaseDate)
            .ThenBy(a => a.Title)
            .Select(a => new AlbumListItemDto
            {
                Id = a.Id,
                Title = a.Title,
                CoverPath = a.CoverPath ?? a.Songs.Select(s => s.CoverPath).FirstOrDefault(),
                ReleaseDate = a.ReleaseDate,
                TracksCount = a.Songs.Count
            })
            .ToListAsync();

        var followersCount = await _db.Follows
            .AsNoTracking()
            .CountAsync(f => f.ArtistUserId == artistUserId && (f.IsActive == null || f.IsActive == true));

        return Ok(new ArtistDetailsDto
        {
            ArtistUserId = artistUserId,
            ArtistName = string.IsNullOrWhiteSpace(artist.ArtistName) ? artist.DisplayName : artist.ArtistName,
            AvatarPath = artist.AvatarPath,
            FollowersCount = followersCount,
            IsFollowing = false,
            TopTracks = topTracks,
            Albums = albums
        });
    }

    [HttpGet("search")]
    public async Task<ActionResult<SearchResponseDto>> Search([FromQuery] string? query, [FromQuery] int take = 20)
    {
        var q = query?.Trim();
        if (string.IsNullOrWhiteSpace(q))
            return Ok(new SearchResponseDto());

        var normalizedTake = Math.Clamp(take, 1, 100);

        var tracks = await _db.Songs
            .AsNoTracking()
            .Where(s => s.IsActive == null || s.IsActive == true)
            .Include(s => s.ArtistUser)
            .Where(s =>
                EF.Functions.Like(s.Title, $"%{q}%") ||
                EF.Functions.Like(s.ArtistUser.DisplayName, $"%{q}%") ||
                (s.ArtistUser.ArtistName != null && EF.Functions.Like(s.ArtistUser.ArtistName, $"%{q}%")))
            .OrderByDescending(s => s.PlayCount)
            .Take(normalizedTake)
            .Select(ToTrackDtoExpr())
            .ToListAsync();

        var albums = await _db.Albums
            .AsNoTracking()
            .Where(a => EF.Functions.Like(a.Title, $"%{q}%"))
            .OrderByDescending(a => a.ReleaseDate)
            .Take(normalizedTake)
            .Select(a => new AlbumListItemDto
            {
                Id = a.Id,
                Title = a.Title,
                CoverPath = a.CoverPath ?? a.Songs.Select(s => s.CoverPath).FirstOrDefault(),
                ReleaseDate = a.ReleaseDate,
                TracksCount = a.Songs.Count
            })
            .ToListAsync();

        var artists = await _db.Users
            .AsNoTracking()
            .Where(u => (u.IsActive == null || u.IsActive == true) && (
                EF.Functions.Like(u.DisplayName, $"%{q}%") ||
                (u.ArtistName != null && EF.Functions.Like(u.ArtistName, $"%{q}%"))))
            .Take(normalizedTake)
            .Select(u => new PublicArtistItemDto
            {
                ArtistUserId = u.Id,
                ArtistName = string.IsNullOrWhiteSpace(u.ArtistName) ? u.DisplayName : u.ArtistName!,
                AvatarPath = u.AvatarPath,
                FollowersCount = u.FollowArtistUsers.Count(f => f.IsActive == null || f.IsActive == true),
                TracksCount = u.Songs.Count(s => s.IsActive == null || s.IsActive == true)
            })
            .ToListAsync();

        var playlists = await _db.Playlists
            .AsNoTracking()
            .Include(p => p.OwnerUser)
            .Where(p => p.IsPublic)
            .Where(p => EF.Functions.Like(p.Title, $"%{q}%") || (p.Description != null && EF.Functions.Like(p.Description, $"%{q}%")))
            .Take(normalizedTake)
            .Select(p => new PublicPlaylistItemDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                CoverPath = p.CoverPath,
                TracksCount = p.Playlistsongs.Count,
                OwnerUserId = p.OwnerUserId,
                OwnerName = p.OwnerUser.DisplayName
            })
            .ToListAsync();

        return Ok(new SearchResponseDto
        {
            Tracks = tracks,
            Albums = albums,
            Artists = artists,
            Playlists = playlists
        });
    }

    [HttpPost("playlists/{playlistId:int}/tracks/bulk-add")]
    public async Task<ActionResult> BulkAddTracksToPlaylist(int playlistId, [FromBody] BulkPlaylistTracksRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var songIds = request.SongIds.Distinct().ToList();
        if (songIds.Count == 0)
            return Ok();

        var existingSongIds = await _db.Playlistsongs
            .Where(ps => ps.PlaylistId == playlistId)
            .Select(ps => ps.SongId)
            .ToListAsync();

        var maxPosition = await _db.Playlistsongs
            .Where(ps => ps.PlaylistId == playlistId)
            .Select(ps => (int?)ps.Position)
            .MaxAsync() ?? 0;

        var validSongIds = await _db.Songs
            .Where(s => songIds.Contains(s.Id) && (s.IsActive == null || s.IsActive == true))
            .Select(s => s.Id)
            .ToListAsync();

        foreach (var songId in validSongIds)
        {
            if (existingSongIds.Contains(songId))
                continue;

            maxPosition++;
            _db.Playlistsongs.Add(new Playlistsong
            {
                PlaylistId = playlistId,
                SongId = songId,
                Position = maxPosition,
                AddedAt = DateTime.UtcNow
            });
        }

        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("playlists/{playlistId:int}/tracks/bulk-remove")]
    public async Task<ActionResult> BulkRemoveTracksFromPlaylist(int playlistId, [FromBody] BulkPlaylistTracksRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var songIds = request.SongIds.Distinct().ToList();
        if (songIds.Count == 0)
            return Ok();

        var items = await _db.Playlistsongs
            .Where(ps => ps.PlaylistId == playlistId && songIds.Contains(ps.SongId))
            .ToListAsync();

        if (items.Count == 0)
            return Ok();

        _db.Playlistsongs.RemoveRange(items);

        var rest = await _db.Playlistsongs
            .Where(ps => ps.PlaylistId == playlistId)
            .OrderBy(ps => ps.Position)
            .ToListAsync();

        await ReindexPlaylistTracksAsync(rest);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("playlists/{playlistId:int}/tracks/reorder-all")]
    public async Task<ActionResult> ReorderAllPlaylistTracks(int playlistId, [FromBody] ReorderPlaylistTracksRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var orderedSongIds = request.SongIdsInOrder.Distinct().ToList();
        var items = await _db.Playlistsongs
            .Where(ps => ps.PlaylistId == playlistId)
            .OrderBy(ps => ps.Position)
            .ToListAsync();

        if (orderedSongIds.Count != items.Count || items.Any(i => !orderedSongIds.Contains(i.SongId)))
            return BadRequest(new ApiErrorResponse { Message = "Передан некорректный состав треков для reorder." });

        var bySong = items.ToDictionary(x => x.SongId);
        var reordered = orderedSongIds.Select(id => bySong[id]).ToList();
        await ReindexPlaylistTracksAsync(reordered);

        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("social/following")]
    public async Task<ActionResult<List<FollowingArtistItemDto>>> GetFollowingArtists()
    {
        var userId = _userContext.GetRequiredUserId(User);

        var items = await _db.Follows
            .AsNoTracking()
            .Where(f => f.SubscriberUserId == userId && (f.IsActive == null || f.IsActive == true))
            .Include(f => f.ArtistUser)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new FollowingArtistItemDto
            {
                ArtistUserId = f.ArtistUserId,
                ArtistName = string.IsNullOrWhiteSpace(f.ArtistUser.ArtistName) ? f.ArtistUser.DisplayName : f.ArtistUser.ArtistName!,
                AvatarPath = f.ArtistUser.AvatarPath,
                FollowedAt = f.CreatedAt,
                FollowersCount = f.ArtistUser.FollowArtistUsers.Count(x => x.IsActive == null || x.IsActive == true)
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("social/followers")]
    public async Task<ActionResult<List<PublicArtistItemDto>>> GetFollowers([FromQuery] int? artistUserId = null)
    {
        var currentUserId = _userContext.GetRequiredUserId(User);
        var targetArtistUserId = artistUserId ?? currentUserId;

        var followers = await _db.Follows
            .AsNoTracking()
            .Where(f => f.ArtistUserId == targetArtistUserId && (f.IsActive == null || f.IsActive == true))
            .Include(f => f.SubscriberUser)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new PublicArtistItemDto
            {
                ArtistUserId = f.SubscriberUserId,
                ArtistName = f.SubscriberUser.DisplayName,
                AvatarPath = f.SubscriberUser.AvatarPath,
                FollowersCount = f.SubscriberUser.FollowArtistUsers.Count(x => x.IsActive == null || x.IsActive == true),
                TracksCount = f.SubscriberUser.Songs.Count(s => s.IsActive == null || s.IsActive == true)
            })
            .ToListAsync();

        return Ok(followers);
    }
    [HttpGet("liked")]
    public async Task<ActionResult<List<TrackListItemDto>>> GetLikedTracks()
    {
        var userId = _userContext.GetRequiredUserId(User);

        var tracks = await _db.Likedsongs
            .AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Include(l => l.Song)
            .ThenInclude(s => s.ArtistUser)
            .Select(l => new TrackListItemDto
            {
                Id = l.Song.Id,
                Title = l.Song.Title,
                Artist = l.Song.ArtistUser.ArtistName != null && l.Song.ArtistUser.ArtistName != "" ? l.Song.ArtistUser.ArtistName : l.Song.ArtistUser.DisplayName,
                DurationSec = l.Song.DurationSec,
                StreamUrl = l.Song.StreamUrl,
                LocalPath = l.Song.LocalPath,
                CoverPath = l.Song.CoverPath ?? (l.Song.Album != null ? l.Song.Album.CoverPath : null),
                ArtistUserId = l.Song.ArtistUserId,
                AlbumId = l.Song.AlbumId
            })
            .ToListAsync();

        return Ok(tracks);
    }

    [HttpPost("liked/{songId:int}")]
    public async Task<ActionResult> LikeTrack(int songId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var exists = await _db.Songs.AnyAsync(s => s.Id == songId);
        if (!exists)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        var already = await _db.Likedsongs.AnyAsync(l => l.UserId == userId && l.SongId == songId);
        if (already)
            return Ok();

        _db.Likedsongs.Add(new Likedsong
        {
            UserId = userId,
            SongId = songId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("liked/{songId:int}")]
    public async Task<ActionResult> UnlikeTrack(int songId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var liked = await _db.Likedsongs.FirstOrDefaultAsync(l => l.UserId == userId && l.SongId == songId);
        if (liked is null)
            return Ok();

        _db.Likedsongs.Remove(liked);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("queue")]
    public async Task<ActionResult<List<QueueItemDto>>> GetQueue()
    {
        var userId = _userContext.GetRequiredUserId(User);

        var queue = await _db.Playbackqueues
            .AsNoTracking()
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.Position)
            .Include(q => q.Song)
            .ThenInclude(s => s.ArtistUser)
            .Select(q => new QueueItemDto
            {
                QueueId = q.Id,
                Position = q.Position,
                Track = new TrackListItemDto
                {
                    Id = q.Song.Id,
                    Title = q.Song.Title,
                    Artist = q.Song.ArtistUser.ArtistName != null && q.Song.ArtistUser.ArtistName != "" ? q.Song.ArtistUser.ArtistName : q.Song.ArtistUser.DisplayName,
                    DurationSec = q.Song.DurationSec,
                    StreamUrl = q.Song.StreamUrl,
                    LocalPath = q.Song.LocalPath,
                    CoverPath = q.Song.CoverPath ?? (q.Song.Album != null ? q.Song.Album.CoverPath : null)
                }
            })
            .ToListAsync();

        return Ok(queue);
    }

    [HttpPost("queue/{songId:int}")]
    public async Task<ActionResult> AddToQueue(int songId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId && (s.IsActive == null || s.IsActive == true));
        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        var maxPosition = await _db.Playbackqueues
            .Where(q => q.UserId == userId)
            .Select(q => (int?)q.Position)
            .MaxAsync() ?? 0;

        _db.Playbackqueues.Add(new Playbackqueue
        {
            UserId = userId,
            SongId = songId,
            Position = maxPosition + 1,
            AddedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("queue/{songId:int}/next")]
    public async Task<ActionResult> AddToQueueNext(int songId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var song = await _db.Songs.FirstOrDefaultAsync(s => s.Id == songId && (s.IsActive == null || s.IsActive == true));
        if (song is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE playbackqueue SET Position = Position + 1 WHERE UserId = {userId}");

        _db.Playbackqueues.Add(new Playbackqueue
        {
            UserId = userId,
            SongId = songId,
            Position = 1,
            AddedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("queue/{queueId:long}")]
    public async Task<ActionResult> RemoveFromQueue(long queueId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var item = await _db.Playbackqueues.FirstOrDefaultAsync(q => q.Id == queueId && q.UserId == userId);
        if (item is null)
            return Ok();

        _db.Playbackqueues.Remove(item);
        await _db.SaveChangesAsync();

        var queue = await _db.Playbackqueues
            .Where(q => q.UserId == userId)
            .OrderBy(q => q.Position)
            .ToListAsync();

        for (var i = 0; i < queue.Count; i++)
            queue[i].Position = i + 1;

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("queue/{queueId:long}/move-up")]
    public async Task<ActionResult> MoveQueueUp(long queueId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var item = await _db.Playbackqueues.FirstOrDefaultAsync(q => q.Id == queueId && q.UserId == userId);
        if (item is null)
            return NotFound(new ApiErrorResponse { Message = "Элемент очереди не найден." });

        var prev = await _db.Playbackqueues
            .Where(q => q.UserId == userId && q.Position < item.Position)
            .OrderByDescending(q => q.Position)
            .FirstOrDefaultAsync();

        if (prev is null)
            return Ok();

        await SwapQueuePositionsAsync(item, prev);
        return Ok();
    }

    [HttpPost("queue/{queueId:long}/move-down")]
    public async Task<ActionResult> MoveQueueDown(long queueId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var item = await _db.Playbackqueues.FirstOrDefaultAsync(q => q.Id == queueId && q.UserId == userId);
        if (item is null)
            return NotFound(new ApiErrorResponse { Message = "Элемент очереди не найден." });

        var next = await _db.Playbackqueues
            .Where(q => q.UserId == userId && q.Position > item.Position)
            .OrderBy(q => q.Position)
            .FirstOrDefaultAsync();

        if (next is null)
            return Ok();

        await SwapQueuePositionsAsync(item, next);
        return Ok();
    }
    [HttpPost("queue/clear")]
    public async Task<ActionResult> ClearQueue()
    {
        var userId = _userContext.GetRequiredUserId(User);
        var items = await _db.Playbackqueues.Where(q => q.UserId == userId).ToListAsync();
        _db.Playbackqueues.RemoveRange(items);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("playlists")]
    public async Task<ActionResult<List<PlaylistListItemDto>>> GetPlaylists()
    {
        var userId = _userContext.GetRequiredUserId(User);

        var playlists = await _db.Playlists
            .AsNoTracking()
            .Where(p => p.OwnerUserId == userId)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new PlaylistListItemDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                IsPublic = p.IsPublic,
                CoverPath = p.CoverPath,
                TracksCount = p.Playlistsongs.Count
            })
            .ToListAsync();

        return Ok(playlists);
    }

    [HttpPost("playlists")]
    public async Task<ActionResult<PlaylistListItemDto>> CreatePlaylist([FromBody] CreatePlaylistRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var entity = new Playlist
        {
            OwnerUserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            CoverPath = request.CoverPath?.Trim(),
            IsPublic = request.IsPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Playlists.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(new PlaylistListItemDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            IsPublic = entity.IsPublic,
            CoverPath = entity.CoverPath,
            TracksCount = 0
        });
    }

    [HttpPut("playlists/{playlistId:int}")]
    public async Task<ActionResult<PlaylistListItemDto>> UpdatePlaylist(int playlistId, [FromBody] UpdatePlaylistRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var playlist = await _db.Playlists
            .Include(p => p.Playlistsongs)
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);

        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        playlist.Title = request.Title.Trim();
        playlist.Description = request.Description?.Trim();
        playlist.CoverPath = request.CoverPath?.Trim();
        playlist.IsPublic = request.IsPublic;
        playlist.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new PlaylistListItemDto
        {
            Id = playlist.Id,
            Title = playlist.Title,
            Description = playlist.Description,
            IsPublic = playlist.IsPublic,
            CoverPath = playlist.CoverPath,
            TracksCount = playlist.Playlistsongs.Count
        });
    }

    [HttpPost("playlists/{playlistId:int}/upload-cover")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<MediaUploadResponseDto>> UploadPlaylistCover(int playlistId, IFormFile file)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        if (file is null || file.Length == 0)
            return BadRequest(new ApiErrorResponse { Message = "Файл не передан." });

        var uploaded = await SaveFormFileAsync(file, "uploads/cover", "playlist", playlistId);
        playlist.CoverPath = uploaded.RelativePath;
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(uploaded);
    }
    [HttpDelete("playlists/{playlistId:int}")]
    public async Task<ActionResult> DeletePlaylist(int playlistId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var items = await _db.Playlistsongs.Where(x => x.PlaylistId == playlistId).ToListAsync();
        _db.Playlistsongs.RemoveRange(items);
        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("playlists/{playlistId:int}")]
    public async Task<ActionResult<PlaylistDetailsDto>> GetPlaylist(int playlistId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var playlist = await _db.Playlists
            .AsNoTracking()
            .Where(p => p.Id == playlistId && p.OwnerUserId == userId)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Description,
                p.IsPublic,
                p.CoverPath,
                TracksCount = p.Playlistsongs.Count
            })
            .FirstOrDefaultAsync();

        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var tracks = await _db.Playlistsongs
            .AsNoTracking()
            .Where(ps => ps.PlaylistId == playlistId)
            .OrderBy(ps => ps.Position)
            .Include(ps => ps.Song)
            .ThenInclude(s => s.ArtistUser)
            .Select(ps => new PlaylistTrackItemDto
            {
                Position = ps.Position,
                Track = new TrackListItemDto
                {
                    Id = ps.Song.Id,
                    Title = ps.Song.Title,
                    Artist = ps.Song.ArtistUser.ArtistName != null && ps.Song.ArtistUser.ArtistName != "" ? ps.Song.ArtistUser.ArtistName : ps.Song.ArtistUser.DisplayName,
                    DurationSec = ps.Song.DurationSec,
                    StreamUrl = ps.Song.StreamUrl,
                    LocalPath = ps.Song.LocalPath,
                    CoverPath = ps.Song.CoverPath ?? (ps.Song.Album != null ? ps.Song.Album.CoverPath : null),
                    ArtistUserId = ps.Song.ArtistUserId,
                    AlbumId = ps.Song.AlbumId
                }
            })
            .ToListAsync();

        return Ok(new PlaylistDetailsDto
        {
            Id = playlist.Id,
            Title = playlist.Title,
            Description = playlist.Description,
            IsPublic = playlist.IsPublic,
            CoverPath = playlist.CoverPath,
            TracksCount = playlist.TracksCount,
            Tracks = tracks
        });
    }

    [HttpPost("playlists/{playlistId:int}/tracks/reorder")]
    public async Task<ActionResult> ReorderPlaylistTrack(int playlistId, [FromBody] ReorderPlaylistTrackRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var tracks = await _db.Playlistsongs
            .Where(ps => ps.PlaylistId == playlistId)
            .OrderBy(ps => ps.Position)
            .ToListAsync();

        var source = tracks.FirstOrDefault(t => t.SongId == request.SongId);
        if (source is null)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден в плейлисте." });

        var normalizedTarget = Math.Clamp(request.TargetPosition, 1, tracks.Count);
        if (source.Position == normalizedTarget)
            return Ok();

        tracks.Remove(source);
        tracks.Insert(normalizedTarget - 1, source);

        await ReindexPlaylistTracksAsync(tracks);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok();
    }
    [HttpGet("playlists/{playlistId:int}/tracks")]
    public async Task<ActionResult<List<TrackListItemDto>>> GetPlaylistTracks(int playlistId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var playlist = await _db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var tracks = await _db.Playlistsongs
            .AsNoTracking()
            .Where(ps => ps.PlaylistId == playlistId)
            .OrderBy(ps => ps.Position)
            .Include(ps => ps.Song)
            .ThenInclude(s => s.ArtistUser)
            .Select(ps => new TrackListItemDto
            {
                Id = ps.Song.Id,
                Title = ps.Song.Title,
                Artist = ps.Song.ArtistUser.ArtistName != null && ps.Song.ArtistUser.ArtistName != "" ? ps.Song.ArtistUser.ArtistName : ps.Song.ArtistUser.DisplayName,
                DurationSec = ps.Song.DurationSec,
                StreamUrl = ps.Song.StreamUrl,
                LocalPath = ps.Song.LocalPath,
                CoverPath = ps.Song.CoverPath ?? (ps.Song.Album != null ? ps.Song.Album.CoverPath : null),
                ArtistUserId = ps.Song.ArtistUserId,
                AlbumId = ps.Song.AlbumId
            })
            .ToListAsync();

        return Ok(tracks);
    }

    [HttpPost("playlists/{playlistId:int}/tracks/{songId:int}")]
    public async Task<ActionResult> AddTrackToPlaylist(int playlistId, int songId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var songExists = await _db.Songs.AnyAsync(s => s.Id == songId && (s.IsActive == null || s.IsActive == true));
        if (!songExists)
            return NotFound(new ApiErrorResponse { Message = "Трек не найден." });

        var duplicate = await _db.Playlistsongs.AnyAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);
        if (duplicate)
            return Ok();

        var maxPosition = await _db.Playlistsongs
            .Where(ps => ps.PlaylistId == playlistId)
            .Select(ps => (int?)ps.Position)
            .MaxAsync() ?? 0;

        _db.Playlistsongs.Add(new Playlistsong
        {
            PlaylistId = playlistId,
            SongId = songId,
            Position = maxPosition + 1,
            AddedAt = DateTime.UtcNow
        });

        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("playlists/{playlistId:int}/tracks/{songId:int}")]
    public async Task<ActionResult> RemoveTrackFromPlaylist(int playlistId, int songId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.Id == playlistId && p.OwnerUserId == userId);
        if (playlist is null)
            return NotFound(new ApiErrorResponse { Message = "Плейлист не найден." });

        var item = await _db.Playlistsongs.FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);
        if (item is null)
            return Ok();

        _db.Playlistsongs.Remove(item);
        playlist.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("artists/{artistUserId:int}/follow")]
    public async Task<ActionResult<FollowArtistStateDto>> FollowArtist(int artistUserId)
    {
        var userId = _userContext.GetRequiredUserId(User);
        if (userId == artistUserId)
            return BadRequest(new ApiErrorResponse { Message = "Нельзя подписаться на самого себя." });

        var artistExists = await _db.Users.AnyAsync(u => u.Id == artistUserId && (u.IsActive == null || u.IsActive == true));
        if (!artistExists)
            return NotFound(new ApiErrorResponse { Message = "Артист не найден." });

        var follow = await _db.Follows.FirstOrDefaultAsync(f => f.SubscriberUserId == userId && f.ArtistUserId == artistUserId);
        if (follow is null)
        {
            _db.Follows.Add(new Follow
            {
                SubscriberUserId = userId,
                ArtistUserId = artistUserId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            follow.IsActive = true;
        }

        await _db.SaveChangesAsync();
        var state = await BuildFollowStateAsync(userId, artistUserId);
        return Ok(state);
    }

    [HttpDelete("artists/{artistUserId:int}/follow")]
    public async Task<ActionResult<FollowArtistStateDto>> UnfollowArtist(int artistUserId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var follow = await _db.Follows.FirstOrDefaultAsync(f => f.SubscriberUserId == userId && f.ArtistUserId == artistUserId);
        if (follow is not null)
        {
            follow.IsActive = false;
            await _db.SaveChangesAsync();
        }

        var state = await BuildFollowStateAsync(userId, artistUserId);
        return Ok(state);
    }

    [HttpGet("artists/{artistUserId:int}/follow-state")]
    public async Task<ActionResult<FollowArtistStateDto>> GetFollowState(int artistUserId)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var state = await BuildFollowStateAsync(userId, artistUserId);
        return Ok(state);
    }

    [HttpGet("artists/{artistUserId:int}")]
    public async Task<ActionResult<ArtistDetailsDto>> GetArtist(int artistUserId)
    {
        var userId = _userContext.GetRequiredUserId(User);

        var artist = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == artistUserId);
        if (artist is null)
            return NotFound(new ApiErrorResponse { Message = "Артист не найден." });

        var topTracks = await _db.Songs
            .AsNoTracking()
            .Where(s => s.ArtistUserId == artistUserId && (s.IsActive == null || s.IsActive == true))
            .Include(s => s.ArtistUser)
            .OrderByDescending(s => s.PlayCount)
            .ThenBy(s => s.Title)
            .Take(20)
            .Select(ToTrackDtoExpr())
            .ToListAsync();

        var albums = await _db.Albums
            .AsNoTracking()
            .Where(a => a.ArtistUserId == artistUserId)
            .OrderByDescending(a => a.ReleaseDate)
            .ThenBy(a => a.Title)
            .Select(a => new AlbumListItemDto
            {
                Id = a.Id,
                Title = a.Title,
                CoverPath = a.CoverPath ?? a.Songs.Select(s => s.CoverPath).FirstOrDefault(),
                ReleaseDate = a.ReleaseDate,
                TracksCount = a.Songs.Count
            })
            .ToListAsync();

        var state = await BuildFollowStateAsync(userId, artistUserId);

        return Ok(new ArtistDetailsDto
        {
            ArtistUserId = artistUserId,
            ArtistName = string.IsNullOrWhiteSpace(artist.ArtistName) ? artist.DisplayName : artist.ArtistName,
            AvatarPath = artist.AvatarPath,
            FollowersCount = state.FollowersCount,
            IsFollowing = state.IsFollowing,
            TopTracks = topTracks,
            Albums = albums
        });
    }

    [HttpGet("albums/{albumId:int}")]
    public async Task<ActionResult<AlbumDetailsDto>> GetAlbum(int albumId)
    {
        var album = await _db.Albums
            .AsNoTracking()
            .Include(a => a.ArtistUser)
            .FirstOrDefaultAsync(a => a.Id == albumId);

        if (album is null)
            return NotFound(new ApiErrorResponse { Message = "Альбом не найден." });

        var tracks = await _db.Songs
            .AsNoTracking()
            .Where(s => s.AlbumId == albumId && (s.IsActive == null || s.IsActive == true))
            .Include(s => s.ArtistUser)
            .OrderBy(s => s.TrackNumber ?? int.MaxValue)
            .ThenBy(s => s.Title)
            .Select(ToTrackDtoExpr())
            .ToListAsync();

        return Ok(new AlbumDetailsDto
        {
            AlbumId = album.Id,
            Title = album.Title,
            ArtistName = string.IsNullOrWhiteSpace(album.ArtistUser.ArtistName) ? album.ArtistUser.DisplayName : album.ArtistUser.ArtistName,
            CoverPath = album.CoverPath ?? album.Songs.Select(s => s.CoverPath).FirstOrDefault(),
            ReleaseDate = album.ReleaseDate,
            Tracks = tracks
        });
    }

    

    [HttpGet("playback/settings")]
    public async Task<ActionResult<PlaybackSettingsDto>> GetPlaybackSettings()
    {
        var userId = _userContext.GetRequiredUserId(User);
        var settings = await GetOrCreatePlaybackSettingsAsync(userId);

        return Ok(new PlaybackSettingsDto
        {
            ShuffleEnabled = settings.ShuffleEnabled,
            RepeatMode = settings.RepeatMode,
            AutoplayEnabled = settings.AutoplayEnabled
        });
    }

    [HttpPut("playback/settings")]
    public async Task<ActionResult<PlaybackSettingsDto>> UpdatePlaybackSettings([FromBody] UpdatePlaybackSettingsRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var repeatMode = NormalizeRepeatMode(request.RepeatMode);
        if (repeatMode is null)
            return BadRequest(new ApiErrorResponse { Message = "Некорректный repeat mode. Допустимые значения: Off, All, One." });

        var settings = await GetOrCreatePlaybackSettingsAsync(userId);
        settings.ShuffleEnabled = request.ShuffleEnabled;
        settings.RepeatMode = repeatMode;
        settings.AutoplayEnabled = request.AutoplayEnabled;
        settings.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new PlaybackSettingsDto
        {
            ShuffleEnabled = settings.ShuffleEnabled,
            RepeatMode = settings.RepeatMode,
            AutoplayEnabled = settings.AutoplayEnabled
        });
    }

    [HttpPost("playback/next")]
    public async Task<ActionResult<NextTrackResponseDto>> GetNextTrack([FromBody] NextTrackRequestDto request)
    {
        var userId = _userContext.GetRequiredUserId(User);
        var settings = await GetOrCreatePlaybackSettingsAsync(userId);
        var repeatMode = NormalizeRepeatMode(settings.RepeatMode) ?? "Off";

        if (repeatMode == "One" && request.CurrentSongId > 0)
        {
            var repeatTrack = await FindTrackByIdAsync(request.CurrentSongId);
            if (repeatTrack is not null)
            {
                return Ok(new NextTrackResponseDto
                {
                    Track = repeatTrack,
                    Source = "RepeatOne",
                    ReachedEnd = false
                });
            }
        }

        if (request.CurrentQueueId.HasValue)
        {
            var currentQueueItem = await _db.Playbackqueues
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == request.CurrentQueueId.Value && q.UserId == userId);

            if (currentQueueItem is not null)
            {
                var nextQueue = await _db.Playbackqueues
                    .AsNoTracking()
                    .Where(q => q.UserId == userId && q.Position > currentQueueItem.Position)
                    .OrderBy(q => q.Position)
                    .Include(q => q.Song)
                    .ThenInclude(s => s.ArtistUser)
                    .Select(q => new QueueItemDto
                    {
                        QueueId = q.Id,
                        Position = q.Position,
                        Track = new TrackListItemDto
                        {
                            Id = q.Song.Id,
                            Title = q.Song.Title,
                            Artist = q.Song.ArtistUser.ArtistName != null && q.Song.ArtistUser.ArtistName != "" ? q.Song.ArtistUser.ArtistName : q.Song.ArtistUser.DisplayName,
                            DurationSec = q.Song.DurationSec,
                            StreamUrl = q.Song.StreamUrl,
                            LocalPath = q.Song.LocalPath,
                            CoverPath = q.Song.CoverPath ?? (q.Song.Album != null ? q.Song.Album.CoverPath : null),
                            ArtistUserId = q.Song.ArtistUserId,
                            AlbumId = q.Song.AlbumId,
                            AlbumTitle = q.Song.Album != null ? q.Song.Album.Title : null
                        }
                    })
                    .FirstOrDefaultAsync();

                if (nextQueue is null && repeatMode == "All")
                {
                    nextQueue = await _db.Playbackqueues
                        .AsNoTracking()
                        .Where(q => q.UserId == userId)
                        .OrderBy(q => q.Position)
                        .Include(q => q.Song)
                        .ThenInclude(s => s.ArtistUser)
                        .Select(q => new QueueItemDto
                        {
                            QueueId = q.Id,
                            Position = q.Position,
                            Track = new TrackListItemDto
                            {
                                Id = q.Song.Id,
                                Title = q.Song.Title,
                                Artist = q.Song.ArtistUser.ArtistName != null && q.Song.ArtistUser.ArtistName != "" ? q.Song.ArtistUser.ArtistName : q.Song.ArtistUser.DisplayName,
                                DurationSec = q.Song.DurationSec,
                                StreamUrl = q.Song.StreamUrl,
                                LocalPath = q.Song.LocalPath,
                                CoverPath = q.Song.CoverPath ?? (q.Song.Album != null ? q.Song.Album.CoverPath : null),
                                ArtistUserId = q.Song.ArtistUserId,
                                AlbumId = q.Song.AlbumId,
                                AlbumTitle = q.Song.Album != null ? q.Song.Album.Title : null
                            }
                        })
                        .FirstOrDefaultAsync();
                }

                if (nextQueue is not null)
                {
                    return Ok(new NextTrackResponseDto
                    {
                        Track = nextQueue.Track,
                        Source = "Queue",
                        ReachedEnd = false
                    });
                }
            }
        }

        if (request.PlaylistId.HasValue)
        {
            var playlistTrackIds = await _db.Playlistsongs
                .AsNoTracking()
                .Where(ps => ps.PlaylistId == request.PlaylistId.Value)
                .OrderBy(ps => ps.Position)
                .Where(ps => ps.Song.IsActive == null || ps.Song.IsActive == true)
                .Select(ps => ps.SongId)
                .ToListAsync();

            var nextPlaylistSongId = ResolveNextSongId(playlistTrackIds, request.CurrentSongId, settings.ShuffleEnabled, repeatMode == "All");
            if (nextPlaylistSongId.HasValue)
            {
                var nextTrack = await FindTrackByIdAsync(nextPlaylistSongId.Value);
                if (nextTrack is not null)
                {
                    return Ok(new NextTrackResponseDto
                    {
                        Track = nextTrack,
                        Source = "Playlist",
                        ReachedEnd = false
                    });
                }
            }
        }

        if (request.AlbumId.HasValue)
        {
            var albumTrackIds = await _db.Songs
                .AsNoTracking()
                .Where(s => s.AlbumId == request.AlbumId.Value && (s.IsActive == null || s.IsActive == true))
                .OrderBy(s => s.TrackNumber ?? int.MaxValue)
                .ThenBy(s => s.Title)
                .Select(s => s.Id)
                .ToListAsync();

            var nextAlbumSongId = ResolveNextSongId(albumTrackIds, request.CurrentSongId, settings.ShuffleEnabled, repeatMode == "All");
            if (nextAlbumSongId.HasValue)
            {
                var nextTrack = await FindTrackByIdAsync(nextAlbumSongId.Value);
                if (nextTrack is not null)
                {
                    return Ok(new NextTrackResponseDto
                    {
                        Track = nextTrack,
                        Source = "Album",
                        ReachedEnd = false
                    });
                }
            }
        }

        if (!settings.AutoplayEnabled)
        {
            return Ok(new NextTrackResponseDto
            {
                Track = null,
                Source = "End",
                ReachedEnd = true
            });
        }

        var fallbackTrack = await _db.Songs
            .AsNoTracking()
            .Where(s => (s.IsActive == null || s.IsActive == true) && s.Id != request.CurrentSongId)
            .Include(s => s.ArtistUser)
            .OrderByDescending(s => s.PlayCount)
            .ThenByDescending(s => s.CreatedAt)
            .Select(ToTrackDtoExpr())
            .FirstOrDefaultAsync();

        if (fallbackTrack is null)
        {
            return Ok(new NextTrackResponseDto
            {
                Track = null,
                Source = "End",
                ReachedEnd = true
            });
        }

        return Ok(new NextTrackResponseDto
        {
            Track = fallbackTrack,
            Source = "Autoplay",
            ReachedEnd = false
        });
    }

    private async Task<Userplaybacksetting> GetOrCreatePlaybackSettingsAsync(int userId)
    {
        var settings = await _db.Userplaybacksettings.FirstOrDefaultAsync(x => x.UserId == userId);
        if (settings is not null)
            return settings;

        settings = new Userplaybacksetting
        {
            UserId = userId,
            ShuffleEnabled = false,
            RepeatMode = "Off",
            AutoplayEnabled = true,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Userplaybacksettings.Add(settings);
        await _db.SaveChangesAsync();

        return settings;
    }

    private static string? NormalizeRepeatMode(string? repeatMode)
    {
        return repeatMode?.Trim().ToLowerInvariant() switch
        {
            "off" => "Off",
            "all" => "All",
            "one" => "One",
            _ => null
        };
    }

    private static int? ResolveNextSongId(IReadOnlyList<int> orderedIds, int currentSongId, bool shuffleEnabled, bool loopAll)
    {
        if (orderedIds.Count == 0)
            return null;

        if (shuffleEnabled)
        {
            var pool = orderedIds.Where(id => id != currentSongId).ToList();
            if (pool.Count == 0)
                return loopAll ? orderedIds[0] : null;

            return pool[Random.Shared.Next(pool.Count)];
        }

        var idx = -1;
        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (orderedIds[i] != currentSongId)
                continue;

            idx = i;
            break;
        }
        if (idx < 0)
            return orderedIds[0];

        var nextIdx = idx + 1;
        if (nextIdx < orderedIds.Count)
            return orderedIds[nextIdx];

        return loopAll ? orderedIds[0] : null;
    }

    private async Task<TrackListItemDto?> FindTrackByIdAsync(int songId)
    {
        return await _db.Songs
            .AsNoTracking()
            .Where(s => s.Id == songId && (s.IsActive == null || s.IsActive == true))
            .Include(s => s.ArtistUser)
            .Select(ToTrackDtoExpr())
            .FirstOrDefaultAsync();
    }
    private async Task<FollowArtistStateDto> BuildFollowStateAsync(int subscriberUserId, int artistUserId)
    {
        var followersCount = await _db.Follows
            .AsNoTracking()
            .Where(f => f.ArtistUserId == artistUserId && (f.IsActive == null || f.IsActive == true))
            .CountAsync();

        var isFollowing = await _db.Follows
            .AsNoTracking()
            .AnyAsync(f => f.SubscriberUserId == subscriberUserId && f.ArtistUserId == artistUserId && (f.IsActive == null || f.IsActive == true));

        return new FollowArtistStateDto
        {
            ArtistUserId = artistUserId,
            IsFollowing = isFollowing,
            FollowersCount = followersCount
        };
    }

    private async Task ReindexPlaylistTracksAsync(List<Playlistsong> tracks)
    {
        for (var i = 0; i < tracks.Count; i++)
            tracks[i].Position = -(i + 1);

        await _db.SaveChangesAsync();

        for (var i = 0; i < tracks.Count; i++)
            tracks[i].Position = i + 1;
    }
    private static Expression<Func<Song, TrackListItemDto>> ToTrackDtoExpr()
    {
        return s => new TrackListItemDto
        {
            Id = s.Id,
            Title = s.Title,
            Artist = s.ArtistUser.ArtistName != null && s.ArtistUser.ArtistName != "" ? s.ArtistUser.ArtistName : s.ArtistUser.DisplayName,
            DurationSec = s.DurationSec,
            StreamUrl = s.StreamUrl,
            LocalPath = s.LocalPath,
            CoverPath = s.CoverPath ?? (s.Album != null ? s.Album.CoverPath : null),
            PlayCount = s.PlayCount > int.MaxValue ? int.MaxValue : (int)s.PlayCount,
            ArtistUserId = s.ArtistUserId,
            AlbumId = s.AlbumId,
            AlbumTitle = s.Album != null ? s.Album.Title : null
        };
    }

    private static async Task<MediaUploadResponseDto> SaveFormFileAsync(IFormFile file, string folder, string entityType, int entityId)
    {
        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{entityType}_{entityId}_{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(folder, fileName).Replace('\\', '/');
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
    private async Task SwapQueuePositionsAsync(Playbackqueue first, Playbackqueue second)
    {
        var firstPosition = first.Position;
        var secondPosition = second.Position;

        first.Position = 0;
        await _db.SaveChangesAsync();

        second.Position = firstPosition;
        first.Position = secondPosition;
        await _db.SaveChangesAsync();
    }
}

















