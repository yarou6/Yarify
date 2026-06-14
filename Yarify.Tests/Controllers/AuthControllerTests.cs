using API.Controllers;
using API.DB;
using API.Models.DTO;
using API.Services.Password;
using API.Services.Token;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Yarify.Tests.TestSupport;

namespace Yarify.Tests.Controllers;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task RegisterReturnsBadRequestPassword1()
    {
        using var db = TestData.CreateDbContext();
        TestData.SeedRolesAndFreePlan(db);

        var controller = CreateController(
            db,
            passwordValidation: new Mock<IPasswordValidationService>().Object,
            passwordHasher: new Mock<IPasswordHasherService>().Object,
            tokenService: new Mock<IAuthTokenService>().Object);

        var result = await controller.Register(new RegisterRequestDto
        {
            Login = "neo",
            Email = "neo@example.com",
            Password = "Abcd1234!",
            ConfirmPassword = "Abcd1234?",
            DisplayName = "Neo"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Пароль и подтверждение пароля не совпадают.", badRequest.Value);
        Assert.Empty(db.Users);
        Assert.Empty(db.Refreshtokens);
    }

    [Fact]
    public async Task RegisterReturnsBadRequestPassword2()
    {
        using var db = TestData.CreateDbContext();
        TestData.SeedRolesAndFreePlan(db);

        var validation = new Mock<IPasswordValidationService>();
        validation.Setup(x => x.ValidatePasswordPolicy("weak"))
            .Returns("Пароль должен содержать минимум 8 символов.");

        var controller = CreateController(
            db,
            validation.Object,
            new Mock<IPasswordHasherService>().Object,
            new Mock<IAuthTokenService>().Object);

        var result = await controller.Register(new RegisterRequestDto
        {
            Login = "neo",
            Email = "neo@example.com",
            Password = "weak",
            ConfirmPassword = "weak",
            DisplayName = "Neo"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Пароль должен содержать минимум 8 символов.", badRequest.Value);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task RegisterReturnsConflictWhenLoginAlreadyExists()
    {
        using var db = TestData.CreateDbContext();
        TestData.SeedRolesAndFreePlan(db);
        SeedExistingUser(db, login: "neo", email: "neo@example.com");

        var controller = CreateController(
            db,
            new Mock<IPasswordValidationService>().Object,
            new Mock<IPasswordHasherService>().Object,
            new Mock<IAuthTokenService>().Object);

        var result = await controller.Register(new RegisterRequestDto
        {
            Login = "neo",
            Email = "neo2@example.com",
            Password = "Abcd1234!",
            ConfirmPassword = "Abcd1234!",
            DisplayName = "Neo Two"
        });

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("Логин уже существует.", conflict.Value);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task RegisterReturnsConflictWhenEmailAlreadyExists()
    {
        using var db = TestData.CreateDbContext();
        TestData.SeedRolesAndFreePlan(db);
        SeedExistingUser(db, login: "neo", email: "neo@example.com");

        var controller = CreateController(
            db,
            new Mock<IPasswordValidationService>().Object,
            new Mock<IPasswordHasherService>().Object,
            new Mock<IAuthTokenService>().Object);

        var result = await controller.Register(new RegisterRequestDto
        {
            Login = "neo-two",
            Email = "neo@example.com",
            Password = "Abcd1234!",
            ConfirmPassword = "Abcd1234!",
            DisplayName = "Neo Two"
        });

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("Email уже существует.", conflict.Value);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task RegisterPersistsUserPlanAndRefreshToken()
    {
        using var db = TestData.CreateDbContext();
        TestData.SeedRolesAndFreePlan(db);

        var passwordValidation = new Mock<IPasswordValidationService>();
        passwordValidation.Setup(x => x.ValidatePasswordPolicy("Abcd1234!"))
            .Returns((string?)null);

        var passwordHasher = new Mock<IPasswordHasherService>();
        passwordHasher.Setup(x => x.Hash("Abcd1234!")).Returns("hashed-password");

        var tokenService = new Mock<IAuthTokenService>();
        tokenService.Setup(x => x.CreateAccessTokenResponseAsync(It.IsAny<User>()))
            .ReturnsAsync(new AuthResponseDto
            {
                Token = "access-token",
                ExpiresAt = new DateTime(2030, 1, 1),
                UserId = 1,
                RoleTitle = "User"
            });
        tokenService.Setup(x => x.CreateRefreshTokenEntity(It.IsAny<int>(), It.IsAny<HttpRequest>(), null))
            .Returns((int userId, HttpRequest request, int? overrideDays) => (
                "refresh-token",
                new Refreshtoken
                {
                    UserId = userId,
                    TokenHash = "refresh-hash",
                    ExpiresAt = new DateTime(2030, 1, 2),
                    CreatedAt = new DateTime(2030, 1, 1),
                    CreatedByIp = request.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = request.Headers.UserAgent.ToString()
                }));

        var controller = CreateController(db, passwordValidation.Object, passwordHasher.Object, tokenService.Object);
        controller.ControllerContext = TestData.CreateControllerContext();

        var result = await controller.Register(new RegisterRequestDto
        {
            Login = "neo",
            Email = "neo@example.com",
            Password = "Abcd1234!",
            ConfirmPassword = "Abcd1234!",
            DisplayName = "Neo"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponseDto>(ok.Value);
        Assert.Equal("access-token", response.Token);
        Assert.Equal("refresh-token", response.RefreshToken);

        Assert.Single(db.Users);
        Assert.Single(db.Userplans);
        Assert.Single(db.Refreshtokens);

        var user = db.Users.Single();
        Assert.Equal("neo", user.Login);
        Assert.Equal("hashed-password", user.PasswordHash);
    }

    [Fact]
    public async Task LoginReturnsUnauthorizedWhenPasswordIsWrong()
    {
        using var db = TestData.CreateDbContext();
        TestData.SeedRolesAndFreePlan(db);
        db.Users.Add(new User
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
        });
        db.SaveChanges();

        var hasher = new Mock<IPasswordHasherService>();
        hasher.Setup(x => x.Verify("wrong", "hashed")).Returns(false);

        var controller = CreateController(
            db,
            new Mock<IPasswordValidationService>().Object,
            hasher.Object,
            new Mock<IAuthTokenService>().Object);

        var result = await controller.Login(new LoginRequestDto
        {
            Login = "neo",
            Password = "wrong"
        });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("Неверный логин или пароль или пользователь неактивен.", unauthorized.Value);
        Assert.Empty(db.Refreshtokens);
    }

    [Fact]
    public async Task LoginUpdatesLastLoginAndCreatesRefreshToken()
    {
        using var db = TestData.CreateDbContext();
        TestData.SeedRolesAndFreePlan(db);
        db.Users.Add(new User
        {
            Id = 1,
            RoleId = 1,
            DisplayName = "Neo",
            Login = "neo",
            Email = "neo@example.com",
            PasswordHash = "hashed",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UnixEpoch
        });
        db.SaveChanges();

        var hasher = new Mock<IPasswordHasherService>();
        hasher.Setup(x => x.Verify("Abcd1234!", "hashed")).Returns(true);

        var tokenService = new Mock<IAuthTokenService>();
        tokenService.Setup(x => x.CreateAccessTokenResponseAsync(It.IsAny<User>()))
            .ReturnsAsync(new AuthResponseDto
            {
                Token = "access-token",
                ExpiresAt = new DateTime(2030, 1, 1),
                UserId = 1,
                RoleTitle = "User"
            });
        tokenService.Setup(x => x.CreateRefreshTokenEntity(It.IsAny<int>(), It.IsAny<HttpRequest>(), null))
            .Returns((int userId, HttpRequest request, int? overrideDays) => (
                "refresh-token",
                new Refreshtoken
                {
                    UserId = userId,
                    TokenHash = "refresh-hash",
                    ExpiresAt = new DateTime(2030, 1, 2),
                    CreatedAt = new DateTime(2030, 1, 1)
                }));

        var controller = CreateController(
            db,
            new Mock<IPasswordValidationService>().Object,
            hasher.Object,
            tokenService.Object);
        controller.ControllerContext = TestData.CreateControllerContext();

        var result = await controller.Login(new LoginRequestDto
        {
            Login = "neo",
            Password = "Abcd1234!"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthResponseDto>(ok.Value);
        Assert.Equal("access-token", response.Token);
        Assert.Single(db.Refreshtokens);

        var user = db.Users.Single();
        Assert.NotNull(user.LastLogin);
        Assert.NotEqual(DateTime.UnixEpoch, user.UpdatedAt);
    }

    private static AuthController CreateController(
        YarifyDbContext db,
        IPasswordValidationService passwordValidation,
        IPasswordHasherService passwordHasher,
        IAuthTokenService tokenService)
    {
        return new AuthController(
            db,
            TestData.CreateJwtConfiguration(),
            passwordValidation,
            passwordHasher,
            tokenService);
    }

    private static void SeedExistingUser(YarifyDbContext db, string login, string email)
    {
        db.Users.Add(new User
        {
            Id = 1,
            RoleId = 1,
            DisplayName = "Neo",
            Login = login,
            Email = email,
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        });
        db.SaveChanges();
    }
}
