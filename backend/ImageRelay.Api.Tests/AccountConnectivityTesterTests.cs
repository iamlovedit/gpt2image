using System.Text.Json;
using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using Xunit;

namespace ImageRelay.Api.Tests;

public class AccountConnectivityTesterTests
{
    [Fact]
    public void BuildRequestBody_UsesFixedResponsesConnectivityPayload()
    {
        var json = JsonSerializer.Serialize(AccountConnectivityTester.BuildRequestBody("mapped-model"));
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("mapped-model", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("you are a helpful assistant", doc.RootElement.GetProperty("instructions").GetString());
        Assert.Equal("auto", doc.RootElement.GetProperty("tool_choice").GetString());
        Assert.True(doc.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("store").GetBoolean());

        var input = doc.RootElement.GetProperty("input");
        Assert.Equal(JsonValueKind.Array, input.ValueKind);
        var item = Assert.Single(input.EnumerateArray());
        Assert.Equal("user", item.GetProperty("role").GetString());
        Assert.Equal("hi", item.GetProperty("content").GetString());
    }

    [Fact]
    public async Task BuildRequest_UsesAccountTokenAndChatGptHeaders()
    {
        using var req = AccountConnectivityTester.BuildRequest(
            new UpstreamAccount
            {
                AccessToken = "access-token",
                ChatGptAccountId = "account-id"
            },
            new UpstreamHeaderSettings
            {
                UserAgent = "ua",
                Version = "v",
                Originator = "origin",
                SessionId = "session"
            },
            "https://chatgpt.com/backend-api/codex/responses",
            "mapped-model");

        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://chatgpt.com/backend-api/codex/responses", req.RequestUri!.ToString());
        Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
        Assert.Equal("access-token", req.Headers.Authorization.Parameter);
        Assert.True(req.Headers.TryGetValues("chatgpt-account-id", out var accountIds));
        Assert.Equal("account-id", Assert.Single(accountIds));
        Assert.True(req.Headers.TryGetValues("session_id", out var sessions));
        Assert.Equal("session", Assert.Single(sessions));

        var body = await req.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("mapped-model", doc.RootElement.GetProperty("model").GetString());
        Assert.Equal("you are a helpful assistant", doc.RootElement.GetProperty("instructions").GetString());
        Assert.Equal("auto", doc.RootElement.GetProperty("tool_choice").GetString());
        Assert.True(doc.RootElement.GetProperty("stream").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("store").GetBoolean());

        var input = doc.RootElement.GetProperty("input");
        Assert.Equal(JsonValueKind.Array, input.ValueKind);
        var item = Assert.Single(input.EnumerateArray());
        Assert.Equal("user", item.GetProperty("role").GetString());
        Assert.Equal("hi", item.GetProperty("content").GetString());
    }
}
