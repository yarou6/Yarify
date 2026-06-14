using API.Controllers;
using API.DB;
using API.Models.DTO;
using API.Services.UserContext;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Yarify.Tests.TestSupport;

namespace Yarify.Tests.Controllers;

public sealed class LibraryControllerTests
{
    [Fact]
    public async Task CreateAlbum()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.CreateAlbum(new CreateAlbumRequestDto
        {
            Title = "  First album  ",
            CoverPath = "  /covers/album.jpg  ",
            ReleaseDate = new DateOnly(2026, 5, 1)
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ManageAlbumDto>(ok.Value);
        Assert.Equal("First album", dto.Title);
        Assert.Equal("/covers/album.jpg", dto.CoverPath);
        Assert.Single(db.Albums);
        Assert.Equal(7, db.Albums.Single().ArtistUserId);
    }

    [Fact]
    public async Task GetMyAlbums()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);

        var albumOne = new Album
        {
            Id = 1,
            ArtistUserId = 7,
            Title = "Beta",
            ReleaseDate = new DateOnly(2024, 1, 1),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var albumTwo = new Album
        {
            Id = 2,
            ArtistUserId = 7,
            Title = "Alpha",
            ReleaseDate = new DateOnly(2024, 1, 1),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var albumThree = new Album
        {
            Id = 3,
            ArtistUserId = 7,
            Title = "Gamma",
            ReleaseDate = new DateOnly(2025, 1, 1),
            CreatedAt = DateTime.UtcNow
        };
        db.Albums.AddRange(albumOne, albumTwo, albumThree);
        db.Songs.Add(new Song
        {
            Id = 10,
            ArtistUserId = 7,
            AlbumId = 3,
            Title = "Track",
            DurationSec = 180,
            SourceType = "Local",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.GetMyAlbums();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var albums = Assert.IsAssignableFrom<List<ManageAlbumDto>>(ok.Value);
        Assert.Equal(new[] { "Gamma", "Alpha", "Beta" }, albums.Select(a => a.Title).ToArray());
        Assert.Equal(1, albums[0].TracksCount);
        Assert.Equal(0, albums[1].TracksCount);
    }

    [Fact]
    public async Task GetMySongs()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);

        var controller = CreateController(db, artistUserId: 7, roles: "User");
        var result = await controller.GetMySongs();

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task CreateSong()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.CreateSong(new CreateSongRequestDto
        {
            Title = "Track",
            DurationSec = 180,
            SourceType = "DVD",
            GenreIds = Array.Empty<int>()
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("SourceType должен быть Local или Online.", error.Message);
        Assert.Empty(db.Songs);
    }

    [Fact]
    public async Task GetMySongsInactive()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);

        var genre = new Genre { Id = 1, Title = "Rock" };
        db.Genres.Add(genre);
        db.Songs.AddRange(
            new Song
            {
                Id = 1,
                ArtistUserId = 7,
                Title = "B song",
                DurationSec = 180,
                SourceType = "Local",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Genres = { genre }
            },
            new Song
            {
                Id = 2,
                ArtistUserId = 7,
                Title = "A song",
                DurationSec = 180,
                SourceType = "Local",
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Genres = { genre }
            });
        db.SaveChanges();

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.GetMySongs();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var songs = Assert.IsAssignableFrom<List<ManageSongDto>>(ok.Value);
        Assert.Single(songs);
        Assert.Equal("B song", songs[0].Title);
        Assert.Single(songs[0].Genres);
    }

    [Fact]
    public async Task DeleteAlbumClearsSongAlbumL()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);

        db.Albums.Add(new Album
        {
            Id = 1,
            ArtistUserId = 7,
            Title = "Album",
            CreatedAt = DateTime.UtcNow
        });
        db.Songs.Add(new Song
        {
            Id = 1,
            ArtistUserId = 7,
            AlbumId = 1,
            Title = "Track",
            DurationSec = 180,
            SourceType = "Local",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.DeleteAlbum(1);

        Assert.IsType<OkResult>(result);
        Assert.Empty(db.Albums);
        Assert.Null(db.Songs.Single().AlbumId);
    }

    [Fact]
    public async Task DeleteAlbumReturnsNotFound()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.DeleteAlbum(999);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Альбом не найден.", error.Message);
    }

    [Fact]
    public async Task CreateSongReturnsBadRequest()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);
        db.Users.Add(new User
        {
            Id = 8,
            RoleId = 2,
            DisplayName = "Other Artist",
            Login = "other-artist",
            Email = "other@example.com",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        });
        db.Albums.Add(new Album
        {
            Id = 99,
            ArtistUserId = 8,
            Title = "Foreign Album",
            ReleaseDate = new DateOnly(2025, 1, 1),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
        db.SaveChanges();

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.CreateSong(new CreateSongRequestDto
        {
            Title = "Track",
            DurationSec = 180,
            SourceType = "Local",
            AlbumId = 99,
            GenreIds = Array.Empty<int>()
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("Альбом не найден или не принадлежит вам.", error.Message);
        Assert.Empty(db.Songs);
    }

    [Fact]
    public async Task CreateSongReturnsBadRequestWhenSomeGenresAreMissing()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);
        db.Genres.Add(new Genre { Id = 1, Title = "Rock" });
        db.SaveChanges();

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.CreateSong(new CreateSongRequestDto
        {
            Title = "Track",
            DurationSec = 180,
            SourceType = "Local",
            GenreIds = new[] { 1, 2 }
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("Некоторые жанры не найдены.", error.Message);
        Assert.Empty(db.Songs);
    }

    [Fact]
    public async Task UpdateSongReturnsNotFound()
    {
        using var db = TestData.CreateDbContext();
        SeedArtist(db);

        var controller = CreateController(db, artistUserId: 7, roles: "Artist");
        var result = await controller.UpdateSong(999, new UpdateSongRequestDto
        {
            Title = "Updated track",
            DurationSec = 200,
            SourceType = "Local"
        });

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("Трек не найден.", error.Message);
    }

    private static LibraryController CreateController(YarifyDbContext db, int artistUserId, params string[] roles)
    {
        var userContext = new Mock<IUserContextService>();
        userContext.Setup(x => x.GetRequiredUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(artistUserId);

        var controller = new LibraryController(db, userContext.Object)
        {
            ControllerContext = TestData.CreateControllerContext(TestData.CreatePrincipal(artistUserId, roles))
        };

        return controller;
    }

    private static void SeedArtist(YarifyDbContext db)
    {
        db.Roles.Add(new Role { Id = 2, Title = "Artist" });
        db.Users.Add(new User
        {
            Id = 7,
            RoleId = 2,
            DisplayName = "Artist One",
            Login = "artist",
            Email = "artist@example.com",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        });
        db.SaveChanges();
    }
}
