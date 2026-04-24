using System.Net.Http.Headers;

namespace ImageRelay.Api.Services;

public sealed record CodexRateLimitHeaders(
    int? PrimaryUsedPercent,
    int? SecondaryUsedPercent,
    int? PrimaryWindowMinutes,
    int? SecondaryWindowMinutes,
    int? PrimaryResetAfterSeconds,
    int? SecondaryResetAfterSeconds,
    DateTime? PrimaryResetAt,
    DateTime? SecondaryResetAt,
    int? PrimaryOverSecondaryLimitPercent)
{
    public bool HasAnyValue =>
        PrimaryUsedPercent.HasValue ||
        SecondaryUsedPercent.HasValue ||
        PrimaryWindowMinutes.HasValue ||
        SecondaryWindowMinutes.HasValue ||
        PrimaryResetAfterSeconds.HasValue ||
        SecondaryResetAfterSeconds.HasValue ||
        PrimaryResetAt.HasValue ||
        SecondaryResetAt.HasValue ||
        PrimaryOverSecondaryLimitPercent.HasValue;
}

public static class CodexRateLimitHeaderParser
{
    public static CodexRateLimitHeaders Parse(HttpResponseHeaders headers) => new(
        ReadInt(headers, "x-codex-primary-used-percent"),
        ReadInt(headers, "x-codex-secondary-used-percent"),
        ReadInt(headers, "x-codex-primary-window-minutes"),
        ReadInt(headers, "x-codex-secondary-window-minutes"),
        ReadInt(headers, "x-codex-primary-reset-after-seconds"),
        ReadInt(headers, "x-codex-secondary-reset-after-seconds"),
        ReadUnixSeconds(headers, "x-codex-primary-reset-at"),
        ReadUnixSeconds(headers, "x-codex-secondary-reset-at"),
        ReadInt(headers, "x-codex-primary-over-secondary-limit-percent"));

    public static bool Apply(UpstreamAccount account, HttpResponseHeaders headers, DateTime nowUtc)
    {
        var parsed = Parse(headers);
        if (!parsed.HasAnyValue) return false;

        ApplyValue(parsed.PrimaryUsedPercent, v => account.CodexPrimaryUsedPercent = v);
        ApplyValue(parsed.SecondaryUsedPercent, v => account.CodexSecondaryUsedPercent = v);
        ApplyValue(parsed.PrimaryWindowMinutes, v => account.CodexPrimaryWindowMinutes = v);
        ApplyValue(parsed.SecondaryWindowMinutes, v => account.CodexSecondaryWindowMinutes = v);
        ApplyValue(parsed.PrimaryResetAfterSeconds, v => account.CodexPrimaryResetAfterSeconds = v);
        ApplyValue(parsed.SecondaryResetAfterSeconds, v => account.CodexSecondaryResetAfterSeconds = v);
        ApplyValue(parsed.PrimaryResetAt, v => account.CodexPrimaryResetAt = v);
        ApplyValue(parsed.SecondaryResetAt, v => account.CodexSecondaryResetAt = v);
        ApplyValue(parsed.PrimaryOverSecondaryLimitPercent, v => account.CodexPrimaryOverSecondaryLimitPercent = v);
        account.CodexRateLimitUpdatedAt = nowUtc;
        return true;
    }

    private static int? ReadInt(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values)) return null;
        var raw = values.FirstOrDefault();
        return int.TryParse(raw, out var value) ? value : null;
    }

    private static DateTime? ReadUnixSeconds(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values)) return null;
        var raw = values.FirstOrDefault();
        if (!long.TryParse(raw, out var seconds)) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static void ApplyValue<T>(T? value, Action<T> apply) where T : struct
    {
        if (value.HasValue) apply(value.Value);
    }
}
