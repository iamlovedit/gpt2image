using System.Text.Json;
using ImageRelay.Api.Features.UpstreamAccounts;
using Xunit;

namespace ImageRelay.Api.Tests;

public class AccountImportParserTests
{
    [Fact]
    public void NormalizeImportItems_ReadsLegacyArrayShape()
    {
        using var doc = JsonDocument.Parse("""
        [
          {"access_token":"access-1","refresh_token":"refresh-1","chatgpt_account_id":"account-1"}
        ]
        """);

        var payload = AccountImportParser.NormalizeImportItems(doc.RootElement);

        Assert.True(payload.IsValidShape);
        var item = Assert.Single(payload.Items);
        Assert.Equal("access-1", item.AccessToken);
        Assert.Equal("refresh-1", item.RefreshToken);
        Assert.Equal("account-1", item.ChatGptAccountId);
    }

    [Fact]
    public void NormalizeImportItems_ReadsExportedAccountsShape()
    {
        using var doc = JsonDocument.Parse("""
        {
          "accounts": [
            {
              "name": "codex_plus_user",
              "notes": "user@example.com",
              "platform": "openai",
              "type": "oauth",
              "proxy_key": "socks5|127.0.0.1|1080|u|p",
              "concurrency": 10,
              "priority": 1,
              "rate_multiplier": 1.5,
              "auto_pause_on_expired": false,
              "extra": { "privacy_mode": "training_off" },
              "credentials": {
                "access_token": "access-2",
                "refresh_token": "refresh-2",
                "chatgpt_account_id": "account-2",
                "chatgpt_user_id": "user-2",
                "client_id": "client-2",
                "organization_id": "org-2",
                "plan_type": "plus",
                "email": "user@example.com",
                "expires_at": "2026-04-30T13:49:29+08:00",
                "subscription_expires_at": 1777478400,
                "model_mapping": { "gpt-4o": "gpt-4o" },
                "_token_version": 2
              }
            }
          ]
        }
        """);

        var payload = AccountImportParser.NormalizeImportItems(doc.RootElement);

        Assert.True(payload.IsValidShape);
        var item = Assert.Single(payload.Items);
        Assert.Equal("access-2", item.AccessToken);
        Assert.Equal("refresh-2", item.RefreshToken);
        Assert.Equal("account-2", item.ChatGptAccountId);
        Assert.Equal("user@example.com", item.Notes);
        Assert.Equal("codex_plus_user", item.Name);
        Assert.Equal("user@example.com", item.Email);
        Assert.Equal("openai", item.Platform);
        Assert.Equal("oauth", item.AccountType);
        Assert.Equal("socks5|127.0.0.1|1080|u|p", item.ProxyKey);
        Assert.Equal(10, item.ConcurrencyLimit);
        Assert.Equal(1, item.Priority);
        Assert.Equal(1.5m, item.RateMultiplier);
        Assert.False(item.AutoPauseOnExpired);
        Assert.Equal("user-2", item.ChatGptUserId);
        Assert.Equal("client-2", item.ClientId);
        Assert.Equal("org-2", item.OrganizationId);
        Assert.Equal("plus", item.PlanType);
        Assert.Equal(new DateTime(2026, 4, 30, 5, 49, 29, DateTimeKind.Utc), item.AccessTokenExpiresAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1777478400).UtcDateTime, item.SubscriptionExpiresAt);
        Assert.Contains("model_mapping", item.RawMetadataJson);
        Assert.Contains("privacy_mode", item.RawMetadataJson);
    }

    [Fact]
    public void NormalizeImportItems_ReadsItemsShapeAndStrategy()
    {
        using var doc = JsonDocument.Parse("""
        {
          "strategy": "Overwrite",
          "items": [
            {
              "accessToken":"access-3",
              "refreshToken":"refresh-3",
              "chatGptUserId":"user-3",
              "clientId":"client-3",
              "organizationId":"org-3",
              "planType":"plus",
              "subscriptionExpiresAt":"2026-04-30T13:49:29+08:00"
            },
            {"accessToken":"","refreshToken":""}
          ]
        }
        """);

        var payload = AccountImportParser.NormalizeImportItems(doc.RootElement);

        Assert.True(payload.IsValidShape);
        Assert.Equal(ImportDuplicateStrategy.Overwrite, payload.Strategy);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal("access-3", payload.Items[0].AccessToken);
        Assert.Equal("user-3", payload.Items[0].ChatGptUserId);
        Assert.Equal("client-3", payload.Items[0].ClientId);
        Assert.Equal("org-3", payload.Items[0].OrganizationId);
        Assert.Equal("plus", payload.Items[0].PlanType);
        Assert.Equal(new DateTime(2026, 4, 30, 5, 49, 29, DateTimeKind.Utc), payload.Items[0].SubscriptionExpiresAt);
    }

    [Fact]
    public void NormalizeImportItems_RejectsMissingItemsShape()
    {
        using var doc = JsonDocument.Parse("{\"accounts\":{}} ");

        var payload = AccountImportParser.NormalizeImportItems(doc.RootElement);

        Assert.False(payload.IsValidShape);
        Assert.Empty(payload.Items);
    }
}
