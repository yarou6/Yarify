using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MVVM.Models.Auth;
using MVVM.Models.Profile;
using MVVM.Services;
using Moq;
using Moq.Protected;

namespace Yarify.Tests.Services;

public sealed class AuthApiClientTests
{
    [Fact]
    public async Task LoginAsyncUsesRememberMeEndpoint()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.PathAndQuery == "/api/auth/login-remember-me"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateJsonResponse(HttpStatusCode.OK, new AuthResponseDto
            {
                Token = "access-token",
                ExpiresAt = new DateTime(2030, 1, 1),
                UserId = 7,
                RoleTitle = "User",
                RefreshToken = "refresh-token",
                RefreshTokenExpiresAt = new DateTime(2030, 2, 1)
            }));

        var client = CreateClient(handler.Object);
        var (data, error) = await client.LoginAsync("neo", "Abcd1234!", rememberMe: true);

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("access-token", data!.Token);
        Assert.Equal("refresh-token", data.RefreshToken);
        handler.VerifyAll();
    }

    [Fact]
    public async Task RefreshAsync()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.PathAndQuery == "/api/session/refresh"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateJsonResponse(HttpStatusCode.Unauthorized, new
            {
                message = "Refresh token already revoked."
            }));

        var client = CreateClient(handler.Object);
        var (data, error) = await client.RefreshAsync("refresh-token");

        Assert.Null(data);
        Assert.Equal("Refresh token already revoked.", error);
        handler.VerifyAll();
    }

    [Fact]
    public async Task SetAccessToken()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.PathAndQuery == "/api/profile/me" &&
                    req.Headers.Authorization != null &&
                    req.Headers.Authorization.Scheme == "Bearer" &&
                    req.Headers.Authorization.Parameter == "jwt-token"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateJsonResponse(HttpStatusCode.OK, new ProfileMeDto
            {
                UserId = 7,
                DisplayName = "Neo",
                Login = "neo",
                Email = "neo@example.com",
                RoleTitle = "User",
                IsActive = true
            }));

        var client = CreateClient(handler.Object);
        client.SetAccessToken("jwt-token");

        var (data, error) = await client.GetProfileMeAsync();

        Assert.Null(error);
        Assert.NotNull(data);
        Assert.Equal("Neo", data!.DisplayName);
        handler.VerifyAll();
    }

    [Fact]
    public async Task GetProfileMeAsync()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("network down"));

        var client = CreateClient(handler.Object);
        var (data, error) = await client.GetProfileMeAsync();

        Assert.Null(data);
        Assert.Equal("network down", error);
    }

    private static AuthApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        return new AuthApiClient(httpClient);
    }

    private static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T payload)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
        };
    }
}
