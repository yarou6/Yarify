using API.Controllers;
using API.DB;
using API.Models.DTO;
using API.Services.Password;
using API.Services.Token;
using API.Services.UserContext;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Yarify.Tests.TestSupport;

namespace Yarify.Tests.Integration;

public sealed class IntegrationTests
{
    [Fact]
    public async Task RegisterRealServices()
    {
        using var db = TestData.CreateDbContext();
        TestData.SeedRolesAndFreePlan(db);
        var config = TestData.CreateJwtConfiguration();
        var tokenService = new AuthTokenService(db, config);

        var controller = new AuthController(
            db,
            config,
            new PasswordValidationService(),
            new PasswordHasherService(),
            tokenService)
        {
            ControllerContext = TestData.CreateControllerContext()
        };

        var result = await controller.Register(new RegisterRequestDto
        {
            Login = "integration-user",
            Email = "integration@example.com",
            Password = "Abcd1234!",
            ConfirmPassword = "Abcd1234!",
            DisplayName = "Integration User"
        });

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Single(db.Users);
        Assert.Single(db.Userplans);
        Assert.Single(db.Refreshtokens);

        var user = db.Users.Single();
        Assert.Equal("integration-user", user.Login);
        Assert.Equal("User", db.Roles.Single(r => r.Id == user.RoleId).Title);
    }

    [Fact]
    public async Task CreateAlbumAndSong()
    {
        using var db = TestData.CreateDbContext();
        SeedLibraryData(db);

        var controller = CreateLibraryController(db);

        var albumResult = await controller.CreateAlbum(new CreateAlbumRequestDto
        {
            Title = "  New album  ",
            ReleaseDate = new DateOnly(2026, 1, 1),
            CoverPath = "  /uploads/cover.jpg  "
        });
        var album = Assert.IsType<ManageAlbumDto>(Assert.IsType<OkObjectResult>(albumResult.Result).Value);

        var songResult = await controller.CreateSong(new CreateSongRequestDto
        {
            AlbumId = album.Id,
            Title = "  First track  ",
            DurationSec = 185,
            SourceType = "Local",
            LocalPath = "  /music/track.mp3  ",
            GenreIds = new[] { 1 }
        });
        var song = Assert.IsType<ManageSongDto>(Assert.IsType<OkObjectResult>(songResult.Result).Value);

        Assert.Equal("First track", song.Title);
        Assert.Single(db.Songs);
        Assert.Single(db.Albums);

        var albumsResult = await controller.GetMyAlbums();
        var albums = Assert.IsAssignableFrom<List<ManageAlbumDto>>(Assert.IsType<OkObjectResult>(albumsResult.Result).Value);
        Assert.Single(albums);
        Assert.Equal(1, albums[0].TracksCount);

        var songsResult = await controller.GetMySongs(includeInactive: true);
        var songs = Assert.IsAssignableFrom<List<ManageSongDto>>(Assert.IsType<OkObjectResult>(songsResult.Result).Value);
        Assert.Single(songs);
        Assert.Single(songs[0].Genres);
        Assert.Equal("Rock", songs[0].Genres[0].Title);
    }

    [Fact]
    public async Task PlayerControllerFiltersAndSortsTracks()
    {
        using var db = TestData.CreateDbContext();
        SeedPlayerData(db);

        var controller = new PlayerController(db, new Mock<IUserContextService>().Object);
        var result = await controller.GetTracks(query: null, genre: "Rock", sort: "duration");

        var tracks = Assert.IsAssignableFrom<List<TrackListItemDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Single(tracks);
        Assert.Equal("Rock track", tracks[0].Title);
    }

    private static LibraryController CreateLibraryController(YarifyDbContext db)
    {
        var userContext = new Mock<IUserContextService>();
        userContext.Setup(x => x.GetRequiredUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).Returns(7);

        return new LibraryController(db, userContext.Object)
        {
            ControllerContext = TestData.CreateControllerContext(TestData.CreatePrincipal(7, "Artist"))
        };
    }

    private static void SeedLibraryData(YarifyDbContext db)
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
        db.Genres.Add(new Genre { Id = 1, Title = "Rock" });
        db.SaveChanges();
    }

    private static void SeedPlayerData(YarifyDbContext db)
    {
        var artist = new User
        {
            Id = 1,
            RoleId = 1,
            DisplayName = "Artist A",
            ArtistName = "Artist A",
            Login = "artist-a",
            Email = "artist@example.com",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };

        db.Users.Add(artist);
        var rock = new Genre { Id = 1, Title = "Rock" };
        var pop = new Genre { Id = 2, Title = "Pop" };
        db.Genres.AddRange(rock, pop);
        db.Songs.AddRange(
            new Song
            {
                Id = 1,
                ArtistUserId = 1,
                Title = "Rock track",
                DurationSec = 240,
                SourceType = "Local",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2),
                Genres = { rock }
            },
            new Song
            {
                Id = 2,
                ArtistUserId = 1,
                Title = "Pop track",
                DurationSec = 120,
                SourceType = "Local",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                Genres = { pop }
            });
        db.SaveChanges();
    }
}
