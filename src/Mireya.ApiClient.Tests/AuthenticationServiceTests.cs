using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Services;
using NSubstitute;

namespace Mireya.ApiClient.Tests;

public class AuthenticationServiceTests
{
    private readonly IMireyaApiClient _api = Substitute.For<IMireyaApiClient>();
    private readonly ICredentialRepository _credentials = Substitute.For<ICredentialRepository>();
    private readonly IBackendManager _backends = Substitute.For<IBackendManager>();
    private readonly IScreenHubService _hub = Substitute.For<IScreenHubService>();

    [Fact]
    public async Task NewBackendWithoutScopedCredentials_IsNotRegistered()
    {
        var backend = CreateBackend();
        _backends.GetCurrentBackendAsync().Returns(backend);
        _credentials.GetCredentialsAsync(backend.Id).Returns((BackendCredential?)null);

        var service = CreateService();

        var state = await service.GetAuthenticationStateAsync();

        Assert.Equal(AuthenticationState.NotRegistered, state);
        await _api.DidNotReceive().GetApiScreenmanagementBonjourAsync();
    }

    [Theory]
    [InlineData(302)]
    [InlineData(401)]
    [InlineData(403)]
    public async Task RejectedStoredToken_RequiresAuthentication(int statusCode)
    {
        var backend = CreateBackend();
        var credential = new BackendCredential
        {
            BackendInstanceId = backend.Id,
            Username = "screen-existing",
            Password = "stored-password",
            AccessToken = "stale-token",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1),
        };

        _backends.GetCurrentBackendAsync().Returns(backend);
        _credentials.GetCredentialsAsync(backend.Id).Returns(credential);
        _credentials.HasValidCredentialsAsync(backend.Id).Returns(true);
        _api.GetApiScreenmanagementBonjourAsync()
            .Returns(_ => Task.FromException<BonjourResponse>(ApiError(statusCode)));

        var service = CreateService();

        var state = await service.GetAuthenticationStateAsync();

        Assert.Equal(AuthenticationState.NotAuthenticated, state);
    }

    [Fact]
    public async Task MissingBackendDisplay_ClearsScopedIdentityAndRegistersAgain()
    {
        var backend = CreateBackend();
        var credential = new BackendCredential
        {
            BackendInstanceId = backend.Id,
            Username = "screen-without-display",
            Password = "stored-password",
            AccessToken = "otherwise-valid-token",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1),
        };

        _backends.GetCurrentBackendAsync().Returns(backend);
        _credentials.GetCredentialsAsync(backend.Id).Returns(credential);
        _credentials.HasValidCredentialsAsync(backend.Id).Returns(true);
        _api.GetApiScreenmanagementBonjourAsync()
            .Returns(_ => Task.FromException<BonjourResponse>(ApiError(404)));

        var service = CreateService();

        var state = await service.GetAuthenticationStateAsync();

        Assert.Equal(AuthenticationState.NotRegistered, state);
        await _credentials.Received(1).DeleteCredentialsAsync(backend.Id);
    }

    [Fact]
    public async Task DeletedBackendUser_IsRegisteredAgainAndLoggedIn()
    {
        var backend = CreateBackend();
        var credential = new BackendCredential
        {
            BackendInstanceId = backend.Id,
            Username = "screen-deleted",
            Password = "old-password",
        };

        _backends.GetCurrentBackendAsync().Returns(backend);
        _credentials.GetCredentialsAsync(backend.Id).Returns(_ => credential);
        _credentials
            .DeleteCredentialsAsync(backend.Id)
            .Returns(_ =>
            {
                credential = null!;
                return Task.CompletedTask;
            });
        _credentials
            .SaveRegistrationAsync(backend.Id, Arg.Any<string>(), Arg.Any<string>())
            .Returns(call =>
            {
                credential = new BackendCredential
                {
                    BackendInstanceId = backend.Id,
                    Username = call.ArgAt<string>(1),
                    Password = call.ArgAt<string>(2),
                };
                return Task.CompletedTask;
            });

        _api.PostLoginAsync(false, false, Arg.Any<LoginRequest>())
            .Returns(
                Task.FromException<AccessTokenResponse>(ApiError(401)),
                Task.FromResult(CreateToken())
            );
        _api.PostApiScreenmanagementRegisterAsync(Arg.Any<RegisterScreenRequest>())
            .Returns(
                new RegisterScreenResponse
                {
                    ScreenIdentifier = "ABC123",
                    UserId = "new-user",
                    ScreenName = "Replacement screen",
                }
            );

        var service = CreateService();

        var result = await service.LoginAsync();

        Assert.True(result.Success);
        await _credentials.Received(1).DeleteCredentialsAsync(backend.Id);
        await _api.Received(1)
            .PostApiScreenmanagementRegisterAsync(Arg.Any<RegisterScreenRequest>());
        await _credentials
            .Received(1)
            .SaveTokensAsync(
                backend.Id,
                "new-access-token",
                "new-refresh-token",
                Arg.Any<DateTime?>()
            );
        await _hub.Received(1).ConnectAsync();
    }

    private AuthenticationService CreateService() =>
        new(_api, _credentials, _backends, _hub, NullLogger<AuthenticationService>.Instance);

    private static BackendInstance CreateBackend() =>
        new()
        {
            Id = Guid.NewGuid(),
            BaseUrl = "https://mireya.example",
            IsCurrentBackend = true,
        };

    private static AccessTokenResponse CreateToken() =>
        new()
        {
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token",
            ExpiresIn = 3600,
            TokenType = "Bearer",
        };

    private static ApiException ApiError(int statusCode) =>
        new(
            $"HTTP {statusCode}",
            statusCode,
            null,
            new Dictionary<string, IEnumerable<string>>(),
            null
        );
}
