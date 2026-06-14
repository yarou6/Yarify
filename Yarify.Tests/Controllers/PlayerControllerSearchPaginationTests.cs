using API.Controllers;
using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Yarify.Tests.TestSupport;

namespace Yarify.Tests.Controllers;

public sealed class PlayerControllerSearchPaginationTests
{
    [Fact]
    public async Task GetRecentHistory_ReturnsNewestItemsAndRespectsTake()
    {
        using var db = TestData.CreateDbContext();
        SeedHistoryData(db);

        var userContext = new Mock<IUserContextService>();
        userContext.Setup(x => x.GetRequiredUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(7);

        var controller = new PlayerController(db, userContext.Object)
        {
            ControllerContext = TestData.CreateControllerContext(TestData.CreatePrincipal(7))
        };

        var result = await controller.GetRecentHistory(take: 2);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsAssignableFrom<List<ListeningHistoryItemDto>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.True(items[0].StartedAt > items[1].StartedAt);
        Assert.Equal(3000, items[0].PlayedMs);
        Assert.Equal(2000, items[1].PlayedMs);
    }

    [Fact]
    public async Task Search_ReturnsMatchingResultsAndAppliesTakeLimit()
    {
        using var db = TestData.CreateDbContext();
        SeedSearchData(db);

        var controller = new PlayerController(db, new Mock<IUserContextService>().Object);

        var result = await controller.Search("Alpha", take: 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SearchResponseDto>(ok.Value);

        Assert.Single(response.Tracks);
        Assert.Equal("Alpha Song High", response.Tracks[0].Title);

        Assert.Single(response.Albums);
        Assert.Equal("Alpha Album New", response.Albums[0].Title);

        Assert.Single(response.Artists);
        Assert.Equal("Alpha Artist", response.Artists[0].ArtistName);

        Assert.Single(response.Playlists);
        Assert.Equal("Alpha Playlist", response.Playlists[0].Title);
    }

    [Fact]
    public async Task GetTracksMapsHugePlayCountToIntMaxValue()
    {
        using var db = TestData.CreateDbContext();
        SeedOverflowData(db);

        var controller = new PlayerController(db, new Mock<IUserContextService>().Object);

        var result = await controller.GetTracks(query: null, genre: null, sort: "plays");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var tracks = Assert.IsAssignableFrom<List<TrackListItemDto>>(ok.Value);

        Assert.Equal(2, tracks.Count);
        Assert.Equal("Overflow Song", tracks[0].Title);
        Assert.Equal(int.MaxValue, tracks[0].PlayCount);
        Assert.Equal("Regular Song", tracks[1].Title);
        Assert.Equal(17, tracks[1].PlayCount);
    }

    [Fact]
    public async Task SearchReturnsEmptyResponse()
    {
        using var db = TestData.CreateDbContext();
        SeedSearchData(db);

        var controller = new PlayerController(db, new Mock<IUserContextService>().Object);

        var result = await controller.Search("   ", take: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SearchResponseDto>(ok.Value);

        Assert.Empty(response.Tracks);
        Assert.Empty(response.Albums);
        Assert.Empty(response.Artists);
        Assert.Empty(response.Playlists);
    }

    [Fact]
    public async Task GetPublicPlaylistReturnsNotFound()
    {
        using var db = TestData.CreateDbContext();
        var controller = new PlayerController(db, new Mock<IUserContextService>().Object);

        var result = await controller.GetPublicPlaylist(999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Публичный плейлист не найден.", error.Message);
    }

    [Fact]
    public async Task GetPublicArtistReturnsNotFound()
    {
        using var db = TestData.CreateDbContext();
        var controller = new PlayerController(db, new Mock<IUserContextService>().Object);

        var result = await controller.GetPublicArtist(999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Артист не найден.", error.Message);
    }

    [Fact]
    public async Task GetPlaylistReturnsNotFound()
    {
        using var db = TestData.CreateDbContext();
        var controller = CreatePlaylistController(db, userId: 7);

        var result = await controller.GetPlaylist(123);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Плейлист не найден.", error.Message);
    }

    [Fact]
    public async Task UpdatePlaylist_ReturnsNotFound_WhenPlaylistDoesNotExist()
    {
        using var db = TestData.CreateDbContext();
        var controller = CreatePlaylistController(db, userId: 7);

        var result = await controller.UpdatePlaylist(123, new UpdatePlaylistRequestDto
        {
            Title = "Updated",
            Description = "Description",
            CoverPath = "/covers/updated.jpg",
            IsPublic = true
        });

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Плейлист не найден.", error.Message);
    }

    [Fact]
    public async Task DeletePlaylist_ReturnsNotFound_WhenPlaylistDoesNotExist()
    {
        using var db = TestData.CreateDbContext();
        var controller = CreatePlaylistController(db, userId: 7);

        var result = await controller.DeletePlaylist(123);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Плейлист не найден.", error.Message);
    }

    private static PlayerController CreatePlaylistController(YarifyDbContext db, int userId)
    {
        var userContext = new Mock<IUserContextService>();
        userContext.Setup(x => x.GetRequiredUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(userId);

        return new PlayerController(db, userContext.Object)
        {
            ControllerContext = TestData.CreateControllerContext(TestData.CreatePrincipal(userId))
        };
    }

    private static void SeedHistoryData(YarifyDbContext db)
    {
        db.Roles.Add(new Role { Id = 1, Title = "User" });
        db.Users.Add(new User
        {
            Id = 7,
            RoleId = 1,
            DisplayName = "History Listener",
            Login = "listener",
            Email = "listener@example.com",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        });

        db.Songs.Add(new Song
        {
            Id = 1,
            ArtistUserId = 7,
            Title = "History Track",
            DurationSec = 180,
            SourceType = "Local",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        });

        var now = DateTime.UtcNow;
        db.Listeningevents.AddRange(
            new Listeningevent
            {
                Id = 1,
                UserId = 7,
                SongId = 1,
                StartedAt = now.AddMinutes(-10),
                EndedAt = now.AddMinutes(-5),
                PlayedMs = 1000,
                Completed = false,
                SourceType = "Direct",
                Song = db.Songs.Local.Single()
            },
            new Listeningevent
            {
                Id = 2,
                UserId = 7,
                SongId = 1,
                StartedAt = now.AddMinutes(-5),
                EndedAt = now.AddMinutes(-4),
                PlayedMs = 2000,
                Completed = true,
                SourceType = "Direct",
                Song = db.Songs.Local.Single()
            },
            new Listeningevent
            {
                Id = 3,
                UserId = 7,
                SongId = 1,
                StartedAt = now,
                EndedAt = now.AddMinutes(1),
                PlayedMs = 3000,
                Completed = true,
                SourceType = "Direct",
                Song = db.Songs.Local.Single()
            });

        db.SaveChanges();
    }

    private static void SeedSearchData(YarifyDbContext db)
    {
        var artist = new User
        {
            Id = 1,
            RoleId = 1,
            DisplayName = "Alpha Artist",
            ArtistName = "Alpha Artist",
            Login = "alpha-artist",
            Email = "artist@example.com",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var otherArtist = new User
        {
            Id = 2,
            RoleId = 1,
            DisplayName = "Beta Artist",
            ArtistName = "Beta Artist",
            Login = "beta-artist",
            Email = "beta@example.com",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-30)
        };

        db.Roles.Add(new Role { Id = 1, Title = "User" });
        db.Users.AddRange(artist, otherArtist);
        db.Genres.Add(new Genre { Id = 1, Title = "Rock" });

        var albumOld = new Album
        {
            Id = 1,
            ArtistUserId = 1,
            Title = "Alpha Album Old",
            ReleaseDate = new DateOnly(2024, 1, 1),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var albumNew = new Album
        {
            Id = 2,
            ArtistUserId = 1,
            Title = "Alpha Album New",
            ReleaseDate = new DateOnly(2025, 1, 1),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var highTrack = new Song
        {
            Id = 1,
            ArtistUserId = 1,
            AlbumId = 2,
            Title = "Alpha Song High",
            DurationSec = 240,
            SourceType = "Local",
            PlayCount = 50,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-3),
            ArtistUser = artist,
            Album = albumNew
        };
        var lowTrack = new Song
        {
            Id = 2,
            ArtistUserId = 1,
            AlbumId = 1,
            Title = "Alpha Song Low",
            DurationSec = 180,
            SourceType = "Local",
            PlayCount = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            ArtistUser = artist,
            Album = albumOld
        };

        highTrack.Genres.Add(db.Genres.Local.Single());
        lowTrack.Genres.Add(db.Genres.Local.Single());

        var playlist = new Playlist
        {
            Id = 1,
            OwnerUserId = 1,
            Title = "Alpha Playlist",
            Description = "Alpha description",
            IsPublic = true,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            OwnerUser = artist
        };

        var playlistSong = new Playlistsong
        {
            Id = 1,
            PlaylistId = 1,
            SongId = 1,
            Position = 1,
            Song = highTrack,
            Playlist = playlist
        };

        db.Albums.AddRange(albumOld, albumNew);
        db.Songs.AddRange(highTrack, lowTrack);
        db.Playlists.Add(playlist);
        db.Playlistsongs.Add(playlistSong);
        db.SaveChanges();
    }

    private static void SeedOverflowData(YarifyDbContext db)
    {
        var artist = new User
        {
            Id = 1,
            RoleId = 1,
            DisplayName = "Overflow Artist",
            ArtistName = "Overflow Artist",
            Login = "overflow-artist",
            Email = "overflow@example.com",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-30)
        };

        db.Roles.Add(new Role { Id = 1, Title = "User" });
        db.Users.Add(artist);

        db.Songs.AddRange(
            new Song
            {
                Id = 1,
                ArtistUserId = 1,
                Title = "Regular Song",
                DurationSec = 180,
                SourceType = "Local",
                PlayCount = 17,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2),
                ArtistUser = artist
            },
            new Song
            {
                Id = 2,
                ArtistUserId = 1,
                Title = "Overflow Song",
                DurationSec = 200,
                SourceType = "Local",
                PlayCount = (long)int.MaxValue + 42,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                ArtistUser = artist
            });

        db.SaveChanges();
    }
}
