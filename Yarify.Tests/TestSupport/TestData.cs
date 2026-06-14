using System.Security.Claims;
using API.DB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Yarify.Tests.TestSupport;

internal static class TestData
{
    public static YarifyDbContext CreateDbContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<YarifyDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .Options;

        return new YarifyDbContext(options);
    }

    public static IConfiguration CreateJwtConfiguration(
        string key = "test-secret-key-1234567890-abcdef-1234567890",
        string issuer = "yarify-tests",
        string audience = "yarify-tests-audience",
        string expiresInMinutes = "120",
        string refreshExpiresInDays = "30",
        string rememberMeDays = "90",
        string resetExpiresInMinutes = "15")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = key,
                ["Jwt:Issuer"] = issuer,
                ["Jwt:Audience"] = audience,
                ["Jwt:ExpiresInMinutes"] = expiresInMinutes,
                ["Jwt:RefreshExpiresInDays"] = refreshExpiresInDays,
                ["Jwt:RememberMeRefreshExpiresInDays"] = rememberMeDays,
                ["Jwt:ResetExpiresInMinutes"] = resetExpiresInMinutes
            })
            .Build();
    }

    public static ClaimsPrincipal CreatePrincipal(int userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, $"user-{userId}")
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    public static ControllerContext CreateControllerContext(ClaimsPrincipal? user = null, string? remoteIp = "127.0.0.1", string? userAgent = "yarify-tests")
    {
        var httpContext = new DefaultHttpContext
        {
            User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
        };

        if (!string.IsNullOrWhiteSpace(remoteIp) && System.Net.IPAddress.TryParse(remoteIp, out var parsedIp))
            httpContext.Connection.RemoteIpAddress = parsedIp;

        if (!string.IsNullOrWhiteSpace(userAgent))
            httpContext.Request.Headers["User-Agent"] = userAgent;

        return new ControllerContext { HttpContext = httpContext };
    }

    public static void SeedRolesAndFreePlan(YarifyDbContext db)
    {
        db.Roles.AddRange(
            new Role { Id = 1, Title = "User" },
            new Role { Id = 2, Title = "Artist" },
            new Role { Id = 3, Title = "Admin" });

        db.Subscriptionplans.Add(new Subscriptionplan
        {
            Id = 1,
            Title = "Free",
            IsFree = true,
            Currency = "USD"
        });

        db.SaveChanges();
    }
}
