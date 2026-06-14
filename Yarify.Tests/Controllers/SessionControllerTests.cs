using API.Controllers;
using API.DB;
using API.Models.DTO;
using API.Services.Token;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Yarify.Tests.TestSupport;

namespace Yarify.Tests.Controllers;

public sealed class SessionControllerTests
{
    [Fact]
    public async Task Refresh_ReturnsUnauthorized()
    {
        using var db = TestData.CreateDbContext();
        var tokenService = new Mock<IAuthTokenService>();
        tokenService.Setup(x => x.HashToken("refresh-token")).Returns("hash");

        var controller = CreateController(db, tokenService.Object);

        var result = await controller.Refresh(new RefreshTokenRequestDto
        {
            RefreshToken = "refresh-token"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("Недействительный refresh token.", unauthorized.Value);
    }

    [Fact]
    public async Task Refresh_RevokesOldTokenAndIssuesNewOne()
    {
        using var db = TestData.CreateDbContext();
        var user = new User
        {
            Id = 1,
            RoleId = 1,
            DisplayName = "Neo",
            Login = "neo",
            Email = "neo@example.com",
            PasswordHash = "hashed",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Users.Add(user);
        db.Refreshtokens.Add(new Refreshtoken
        {
            Id = 1,
            UserId = user.Id,
            User = user,
            TokenHash = "old-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        db.SaveChanges();

        var tokenService = new Mock<IAuthTokenService>();
        tokenService.Setup(x => x.HashToken("refresh-token")).Returns("old-hash");
        tokenService.Setup(x => x.CreateRefreshTokenEntity(user.Id, It.IsAny<HttpRequest>(), null))
            .Returns((int userId, HttpRequest request, int? overrideDays) => (
                "new-raw-token",
                new Refreshtoken
                {
                    UserId = userId,
                    TokenHash = "new-hash",
                    ExpiresAt = DateTime.UtcNow.AddDays(30),
                    CreatedAt = DateTime.UtcNow,
                    CreatedByIp = request.HttpContext.Connection.RemoteIpAddress?.ToString()
                }));
        tokenService.Setup(x => x.CreateAccessTokenResponseAsync(It.IsAny<User>()))
            .ReturnsAsync(new AuthResponseDto
            {
                Token = "new-access-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                UserId = user.Id,
                RoleTitle = "User"
            });

        var controller = CreateController(db, tokenService.Object);
        controller.ControllerContext = TestData.CreateControllerContext(remoteIp: "203.0.113.4");

        var result = await controller.Refresh(new RefreshTokenRequestDto
        {
            RefreshToken = "refresh-token"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponseDto>(ok.Value);
        Assert.Equal("new-raw-token", response.RefreshToken);
        Assert.Equal("new-access-token", response.Token);

        var storedToken = db.Refreshtokens.Single(t => t.TokenHash == "old-hash");
        Assert.NotNull(storedToken.RevokedAt);
        Assert.Equal("203.0.113.4", storedToken.RevokedByIp);
        Assert.Equal("new-hash", storedToken.ReplacedByTokenHash);
        Assert.Equal(2, db.Refreshtokens.Count());
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorizedWhenUserIsInactive()
    {
        using var db = TestData.CreateDbContext();
        var user = new User
        {
            Id = 1,
            RoleId = 1,
            DisplayName = "Neo",
            Login = "neo",
            Email = "neo@example.com",
            PasswordHash = "hashed",
            IsActive = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.Users.Add(user);
        db.Refreshtokens.Add(new Refreshtoken
        {
            UserId = user.Id,
            User = user,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        db.SaveChanges();

        var tokenService = new Mock<IAuthTokenService>();
        tokenService.Setup(x => x.HashToken("refresh-token")).Returns("hash");

        var controller = CreateController(db, tokenService.Object);

        var result = await controller.Refresh(new RefreshTokenRequestDto
        {
            RefreshToken = "refresh-token"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("Пользователь неактивен.", unauthorized.Value);
        Assert.Single(db.Refreshtokens);
    }

    private static SessionController CreateController(YarifyDbContext db, IAuthTokenService tokenService)
    {
        return new SessionController(db, tokenService);
    }
}
