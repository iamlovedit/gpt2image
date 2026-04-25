using ImageRelay.Api.Configuration;
using ImageRelay.Api.Data;
using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using Xunit;

namespace ImageRelay.Api.Tests;

public class TokenRefresherTests
{
    [Fact]
    public async Task BuildRefreshRequest_UsesOpenAiAuthFormPayloadAndConfiguredUserAgent()
    {
        using var req = TokenRefresher.BuildRefreshRequest(
            "https://auth.openai.com/oauth/token",
            "client-id",
            "refresh-token",
            new UpstreamHeaderSettings { UserAgent = " configured-agent/1.2 " });

        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://auth.openai.com/oauth/token", req.RequestUri!.ToString());
        Assert.True(req.Headers.TryGetValues("user-agent", out var userAgents));
        Assert.Equal("configured-agent/1.2", Assert.Single(userAgents));
        Assert.Equal("application/x-www-form-urlencoded", req.Content!.Headers.ContentType!.MediaType);

        var body = await req.Content.ReadAsStringAsync();
        var fields = body.Split('&')
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0].Replace('+', ' ')),
                part => Uri.UnescapeDataString(part[1].Replace('+', ' ')));

        Assert.Equal("refresh_token", fields["grant_type"]);
        Assert.Equal("refresh-token", fields["refresh_token"]);
        Assert.Equal("client-id", fields["client_id"]);
        Assert.Equal("openid profile email offline_access", fields["scope"]);
        Assert.DoesNotContain("redirect_uri", fields.Keys);
    }

    [Fact]
    public void BuildRefreshRequest_WhenConfiguredUserAgentBlank_UsesDefaultUserAgent()
    {
        using var req = TokenRefresher.BuildRefreshRequest(
            "https://auth.openai.com/oauth/token",
            "client-id",
            "refresh-token",
            new UpstreamHeaderSettings { UserAgent = "   " });

        Assert.True(req.Headers.TryGetValues("user-agent", out var userAgents));
        Assert.Equal(UpstreamHeaderSettings.DefaultUserAgent, Assert.Single(userAgents));
    }

    [Fact]
    public async Task EnsureFreshAsync_UsesAccountClientIdInRefreshRequest()
    {
        var account = new UpstreamAccount
        {
            AccessToken = "old-access",
            RefreshToken = "refresh-token",
            ClientId = " account-client-id ",
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            Status = UpstreamAccountStatus.Healthy
        };
        await using var services = CreateServices(account);
        var httpFactory = new RecordingHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {"access_token":"new-access","refresh_token":"new-refresh","expires_in":1800}
            """, Encoding.UTF8, "application/json")
        });
        var refresher = CreateRefresher(services, httpFactory);

        await refresher.EnsureFreshAsync(account, force: true, CancellationToken.None);

        var fields = Assert.Single(httpFactory.FormRequests);
        Assert.Equal("account-client-id", fields["client_id"]);
        Assert.Equal("new-access", account.AccessToken);
        Assert.Equal("new-refresh", account.RefreshToken);
    }

    [Fact]
    public async Task EnsureFreshAsync_UsesConfiguredHeaderSettingsInRefreshRequest()
    {
        var account = new UpstreamAccount
        {
            AccessToken = "old-access",
            RefreshToken = "refresh-token",
            ClientId = "client-id",
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            Status = UpstreamAccountStatus.Healthy
        };
        await using var services = CreateServices(
            account,
            new UpstreamHeaderSettings { UserAgent = "system-agent/2.0" });
        var httpFactory = new RecordingHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {"access_token":"new-access","refresh_token":"new-refresh","expires_in":1800}
            """, Encoding.UTF8, "application/json")
        });
        var refresher = CreateRefresher(services, httpFactory);

        await refresher.EnsureFreshAsync(account, force: true, CancellationToken.None);

        var request = Assert.Single(httpFactory.Requests);
        Assert.True(request.Headers.TryGetValues("user-agent", out var userAgents));
        Assert.Equal("system-agent/2.0", Assert.Single(userAgents));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EnsureFreshAsync_WhenClientIdMissing_MarksInvalidAndDoesNotSendRequest(string? clientId)
    {
        var account = new UpstreamAccount
        {
            AccessToken = "old-access",
            RefreshToken = "refresh-token",
            ClientId = clientId,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            Status = UpstreamAccountStatus.Healthy
        };
        await using var services = CreateServices(account);
        var httpFactory = new RecordingHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK));
        var refresher = CreateRefresher(services, httpFactory);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            refresher.EnsureFreshAsync(account, force: true, CancellationToken.None));

        Assert.Equal("refresh failed: missing client_id", ex.Message);
        Assert.Empty(httpFactory.Requests);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.UpstreamAccounts.FindAsync(account.Id);
        Assert.NotNull(saved);
        Assert.Equal(UpstreamAccountStatus.Invalid, saved.Status);
        Assert.Equal("refresh failed: missing client_id", saved.LastError);
        Assert.Equal(1, saved.FailureCount);
    }

    [Theory]
    [InlineData("client-id", "client-id")]
    [InlineData(" client-id ", "client-id")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void NormalizeClientId_TrimsAndRejectsMissingValues(string input, string? expected)
    {
        Assert.Equal(expected, TokenRefresher.NormalizeClientId(input));
    }

    [Fact]
    public void ApplySuccessfulRefresh_UpdatesTokensExpiryAndRestoresHealthyStatus()
    {
        var before = DateTime.UtcNow;
        var account = new UpstreamAccount
        {
            AccessToken = "old-access",
            RefreshToken = "old-refresh",
            Status = UpstreamAccountStatus.Cooling,
            CoolingUntil = before.AddMinutes(5),
            LastError = "old error"
        };

        TokenRefresher.ApplySuccessfulRefresh(account, "new-access", "new-refresh", 1800);

        Assert.Equal("new-access", account.AccessToken);
        Assert.Equal("new-refresh", account.RefreshToken);
        Assert.Equal(UpstreamAccountStatus.Healthy, account.Status);
        Assert.Null(account.CoolingUntil);
        Assert.Null(account.LastError);
        Assert.True(account.AccessTokenExpiresAt >= before.AddMinutes(29));
    }

    [Fact]
    public void ApplySuccessfulRefresh_KeepsExistingRefreshTokenWhenResponseOmitsOne()
    {
        var account = new UpstreamAccount
        {
            AccessToken = "old-access",
            RefreshToken = "old-refresh",
            Status = UpstreamAccountStatus.Healthy
        };

        TokenRefresher.ApplySuccessfulRefresh(account, "new-access", null, 0);

        Assert.Equal("new-access", account.AccessToken);
        Assert.Equal("old-refresh", account.RefreshToken);
        Assert.True(account.AccessTokenExpiresAt > DateTime.UtcNow.AddMinutes(59));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void MarkHttpRefreshFailure_MapsCredentialFailuresToInvalid(HttpStatusCode statusCode)
    {
        var account = new UpstreamAccount
        {
            Status = UpstreamAccountStatus.Healthy,
            CoolingUntil = DateTime.UtcNow.AddMinutes(5)
        };

        TokenRefresher.MarkHttpRefreshFailure(account, statusCode, "bad token", coolingMinutes: 7);

        Assert.Equal(UpstreamAccountStatus.Invalid, account.Status);
        Assert.Null(account.CoolingUntil);
        Assert.Contains($"HTTP {(int)statusCode}", account.LastError);
        Assert.Equal(1, account.FailureCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public void MarkHttpRefreshFailure_MapsTemporaryFailuresToCooling(HttpStatusCode statusCode)
    {
        var before = DateTime.UtcNow;
        var account = new UpstreamAccount { Status = UpstreamAccountStatus.Healthy };

        TokenRefresher.MarkHttpRefreshFailure(account, statusCode, "temporary", coolingMinutes: 7);

        Assert.Equal(UpstreamAccountStatus.Cooling, account.Status);
        Assert.NotNull(account.CoolingUntil);
        Assert.True(account.CoolingUntil > before.AddMinutes(6));
        Assert.Contains($"HTTP {(int)statusCode}", account.LastError);
        Assert.Equal(1, account.FailureCount);
    }

    [Fact]
    public void MarkTemporaryRefreshFailure_UsesAtLeastOneMinuteCooling()
    {
        var before = DateTime.UtcNow;
        var account = new UpstreamAccount { Status = UpstreamAccountStatus.Healthy };

        TokenRefresher.MarkTemporaryRefreshFailure(account, "network", coolingMinutes: 0);

        Assert.Equal(UpstreamAccountStatus.Cooling, account.Status);
        Assert.True(account.CoolingUntil > before.AddSeconds(50));
        Assert.Equal("network", account.LastError);
        Assert.Equal(1, account.FailureCount);
    }

    private static ServiceProvider CreateServices(
        UpstreamAccount account,
        UpstreamHeaderSettings? headerSettings = null)
    {
        var dbRoot = new InMemoryDatabaseRoot();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("token-refresher-tests-" + Guid.NewGuid(), dbRoot)
            .Options;
        var services = new ServiceCollection();
        services.AddSingleton(dbOptions);
        services.AddScoped<AppDbContext>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UpstreamAccounts.Add(account);
        if (headerSettings is not null) db.UpstreamHeaderSettings.Add(headerSettings);
        db.SaveChanges();
        return provider;
    }

    private static TokenRefresher CreateRefresher(
        IHttpClientFactory httpFactory,
        IServiceProvider services) =>
        new(
            httpFactory,
            Options.Create(new UpstreamOptions
            {
                TokenUrl = "https://auth.openai.com/oauth/token"
            }),
            Options.Create(new ProxyOptions { CoolingMinutes = 7, RefreshSkewSeconds = 300 }),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TokenRefresher>.Instance);

    private static TokenRefresher CreateRefresher(
        ServiceProvider services,
        IHttpClientFactory httpFactory) =>
        CreateRefresher(httpFactory, services);

    private static async Task<Dictionary<string, string>> ReadFormFieldsAsync(HttpContent content)
    {
        var body = await content.ReadAsStringAsync();
        return body.Split('&')
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                part => Uri.UnescapeDataString(part[0].Replace('+', ' ')),
                part => Uri.UnescapeDataString(part[1].Replace('+', ' ')));
    }

    private sealed class RecordingHttpClientFactory(HttpResponseMessage response) : IHttpClientFactory
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<Dictionary<string, string>> FormRequests { get; } = [];

        public HttpClient CreateClient(string name) => new(new RecordingHandler(this, response));
    }

    private sealed class RecordingHandler(RecordingHttpClientFactory factory, HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            factory.Requests.Add(request);
            if (request.Content is not null)
                factory.FormRequests.Add(ReadFormFieldsAsync(request.Content).GetAwaiter().GetResult());
            return Task.FromResult(response);
        }
    }
}
