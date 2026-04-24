using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using System.Text.Json;
using Xunit;

namespace ImageRelay.Api.Tests;

public class UpstreamForwarderTests
{
    [Fact]
    public void ResolveNoLeaseFailure_WhenNoAccountWasAttempted_Returns503()
    {
        var failure = UpstreamForwarder.ResolveNoLeaseFailure(false, null);

        Assert.Equal(503, failure.HttpStatus);
        Assert.Equal("no available upstream account", failure.Message);
        Assert.Equal(RequestBusinessStatus.NoAvailableAccount, failure.BusinessStatus);
        Assert.Equal("NoHealthyAccount", failure.ErrorType);
    }

    [Fact]
    public void ResolveNoLeaseFailure_WhenAccountsWereAttempted_Returns502()
    {
        var failure = UpstreamForwarder.ResolveNoLeaseFailure(true, null);

        Assert.Equal(502, failure.HttpStatus);
        Assert.Equal("all upstream attempts failed", failure.Message);
        Assert.Equal(RequestBusinessStatus.UpstreamError, failure.BusinessStatus);
        Assert.Equal("ExhaustedHealthyAccounts", failure.ErrorType);
    }

    [Fact]
    public void ResolveNoLeaseFailure_WhenAccountsWereAttempted_PreservesExistingErrorType()
    {
        var failure = UpstreamForwarder.ResolveNoLeaseFailure(true, "UpstreamError");

        Assert.Equal(502, failure.HttpStatus);
        Assert.Equal("UpstreamError", failure.ErrorType);
    }

    [Fact]
    public async Task BuildUpstreamRequest_UsesCurrentAccountTokenWithoutRefreshPrecondition()
    {
        using var req = UpstreamForwarder.BuildUpstreamRequest(
            new UpstreamAccount
            {
                AccessToken = "current-access-token",
                ChatGptAccountId = "account-id"
            },
            new UpstreamHeaderSettings
            {
                UserAgent = "ua",
                Version = "v",
                Originator = "origin",
                SessionId = "session-id"
            },
            JsonSerializer.Serialize(new { model = "mapped-model" }),
            "https://chatgpt.com/backend-api/codex/responses");

        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://chatgpt.com/backend-api/codex/responses", req.RequestUri!.ToString());
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("current-access-token", req.Headers.Authorization.Parameter);
        Assert.Contains(req.Headers.Accept, x => x.MediaType == "text/event-stream");
        Assert.True(req.Headers.TryGetValues("chatgpt-account-id", out var accountIds));
        Assert.Equal("account-id", Assert.Single(accountIds));

        var body = await req.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("mapped-model", doc.RootElement.GetProperty("model").GetString());
    }
}
