using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using System.Net;
using Xunit;

namespace ImageRelay.Api.Tests;

public class CodexRateLimitHeaderParserTests
{
    [Fact]
    public void Apply_WithCodexHeaders_UpdatesAccountFields()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("x-codex-primary-used-percent", "6");
        response.Headers.TryAddWithoutValidation("x-codex-secondary-used-percent", "8");
        response.Headers.TryAddWithoutValidation("x-codex-primary-window-minutes", "300");
        response.Headers.TryAddWithoutValidation("x-codex-secondary-window-minutes", "10080");
        response.Headers.TryAddWithoutValidation("x-codex-primary-reset-after-seconds", "11679");
        response.Headers.TryAddWithoutValidation("x-codex-secondary-reset-after-seconds", "487086");
        response.Headers.TryAddWithoutValidation("x-codex-primary-reset-at", "1776942931");
        response.Headers.TryAddWithoutValidation("x-codex-secondary-reset-at", "1777418338");
        response.Headers.TryAddWithoutValidation("x-codex-primary-over-secondary-limit-percent", "0");
        var account = new UpstreamAccount();
        var now = new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc);

        var changed = CodexRateLimitHeaderParser.Apply(account, response.Headers, now);

        Assert.True(changed);
        Assert.Equal(6, account.CodexPrimaryUsedPercent);
        Assert.Equal(8, account.CodexSecondaryUsedPercent);
        Assert.Equal(300, account.CodexPrimaryWindowMinutes);
        Assert.Equal(10080, account.CodexSecondaryWindowMinutes);
        Assert.Equal(11679, account.CodexPrimaryResetAfterSeconds);
        Assert.Equal(487086, account.CodexSecondaryResetAfterSeconds);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1776942931).UtcDateTime, account.CodexPrimaryResetAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1777418338).UtcDateTime, account.CodexSecondaryResetAt);
        Assert.Equal(0, account.CodexPrimaryOverSecondaryLimitPercent);
        Assert.Equal(now, account.CodexRateLimitUpdatedAt);
    }

    [Fact]
    public void Apply_WithoutCodexHeaders_DoesNotUpdateExistingAccountFields()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        var originalUpdatedAt = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc);
        var account = new UpstreamAccount
        {
            CodexPrimaryUsedPercent = 12,
            CodexRateLimitUpdatedAt = originalUpdatedAt
        };

        var changed = CodexRateLimitHeaderParser.Apply(account, response.Headers, DateTime.UtcNow);

        Assert.False(changed);
        Assert.Equal(12, account.CodexPrimaryUsedPercent);
        Assert.Equal(originalUpdatedAt, account.CodexRateLimitUpdatedAt);
    }

    [Fact]
    public void Apply_WithInvalidHeaders_IgnoresInvalidValuesAndDoesNotThrow()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("x-codex-primary-used-percent", "not-a-number");
        response.Headers.TryAddWithoutValidation("x-codex-primary-reset-at", "999999999999999999999");
        response.Headers.TryAddWithoutValidation("x-codex-secondary-used-percent", "8");
        var account = new UpstreamAccount();
        var now = new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc);

        var changed = CodexRateLimitHeaderParser.Apply(account, response.Headers, now);

        Assert.True(changed);
        Assert.Null(account.CodexPrimaryUsedPercent);
        Assert.Null(account.CodexPrimaryResetAt);
        Assert.Equal(8, account.CodexSecondaryUsedPercent);
        Assert.Equal(now, account.CodexRateLimitUpdatedAt);
    }
}
