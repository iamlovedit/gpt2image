using ImageRelay.Api.Configuration;
using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace ImageRelay.Api.Tests;

public class AccountConnectivityStatusUpdaterTests
{
    private static AccountConnectivityStatusUpdater CreateUpdater() =>
        new(Options.Create(new ProxyOptions { CoolingMinutes = 7 }));

    [Fact]
    public void MarkSuccess_SetsHealthyAndClearsError()
    {
        var updater = CreateUpdater();
        var account = new UpstreamAccount
        {
            Status = UpstreamAccountStatus.Invalid,
            LastError = "old error",
            CoolingUntil = DateTime.UtcNow.AddMinutes(5),
            SuccessCount = 2
        };

        updater.MarkSuccess(account);

        Assert.Equal(UpstreamAccountStatus.Healthy, account.Status);
        Assert.Null(account.LastError);
        Assert.Null(account.CoolingUntil);
        Assert.NotNull(account.LastUsedAt);
        Assert.Equal(3, account.SuccessCount);
    }

    [Fact]
    public void MarkHttpFailure_MapsUnauthorizedToInvalid()
    {
        var account = new UpstreamAccount { Status = UpstreamAccountStatus.Healthy };

        CreateUpdater().MarkHttpFailure(account, StatusCodes.Status401Unauthorized, "unauthorized");

        Assert.Equal(UpstreamAccountStatus.Invalid, account.Status);
        Assert.Contains("HTTP 401", account.LastError);
        Assert.Equal(1, account.FailureCount);
    }

    [Theory]
    [InlineData(StatusCodes.Status402PaymentRequired)]
    [InlineData(StatusCodes.Status403Forbidden)]
    public void MarkHttpFailure_MapsPaymentOrForbiddenToBanned(int statusCode)
    {
        var account = new UpstreamAccount { Status = UpstreamAccountStatus.Healthy };

        CreateUpdater().MarkHttpFailure(account, statusCode, "forbidden");

        Assert.Equal(UpstreamAccountStatus.Banned, account.Status);
        Assert.Contains($"HTTP {statusCode}", account.LastError);
    }

    [Fact]
    public void MarkHttpFailure_MapsRateLimitToCooling()
    {
        var before = DateTime.UtcNow;
        var account = new UpstreamAccount { Status = UpstreamAccountStatus.Healthy };

        CreateUpdater().MarkHttpFailure(account, StatusCodes.Status429TooManyRequests, "rate limited");

        Assert.Equal(UpstreamAccountStatus.Cooling, account.Status);
        Assert.NotNull(account.CoolingUntil);
        Assert.True(account.CoolingUntil > before.AddMinutes(6));
        Assert.Contains("HTTP 429", account.LastError);
    }

    [Fact]
    public void MarkNetworkFailure_KeepsCurrentStatus()
    {
        var account = new UpstreamAccount { Status = UpstreamAccountStatus.Healthy };

        CreateUpdater().MarkNetworkFailure(account, "dns failed");

        Assert.Equal(UpstreamAccountStatus.Healthy, account.Status);
        Assert.Contains("NetworkError", account.LastError);
        Assert.Equal(1, account.FailureCount);
    }

    [Fact]
    public void MarkRefreshFailure_SetsInvalid()
    {
        var account = new UpstreamAccount { Status = UpstreamAccountStatus.Healthy };

        CreateUpdater().MarkRefreshFailure(account, "bad refresh token");

        Assert.Equal(UpstreamAccountStatus.Invalid, account.Status);
        Assert.Contains("refresh failed", account.LastError);
        Assert.Equal(1, account.FailureCount);
    }
}
