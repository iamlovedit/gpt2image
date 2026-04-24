using ImageRelay.Api.Data.Entities;

namespace ImageRelay.Api.Features.Dashboard;

public static class RequestStatsService
{
    private static readonly TimeZoneInfo ChinaTimeZone = ResolveChinaTimeZone();

    public static RequestStatsRange? CreateRange(string? range, DateTime nowUtc)
    {
        var normalized = NormalizeRange(range);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), ChinaTimeZone);
        var localToday = localNow.Date;

        return normalized switch
        {
            "today" => new RequestStatsRange(
                normalized,
                "hour",
                ToUtc(localToday),
                nowUtc,
                localNow.Hour + 1),
            "7d" => new RequestStatsRange(
                normalized,
                "day",
                ToUtc(localToday.AddDays(-6)),
                nowUtc,
                7),
            "30d" => new RequestStatsRange(
                normalized,
                "day",
                ToUtc(localToday.AddDays(-29)),
                nowUtc,
                30),
            _ => null
        };
    }

    public static RequestStatsResponse Build(
        RequestStatsRange range,
        IReadOnlyCollection<RequestLog> logs,
        IReadOnlyDictionary<Guid, string> accountNames,
        IReadOnlyDictionary<Guid, string> clientKeyNames)
    {
        var buckets = CreateBuckets(range)
            .Select(bucketStart => BuildBucket(range, bucketStart, logs.Where(log => IsInBucket(range, log.StartedAt, bucketStart))))
            .ToList();

        var statusBreakdown = logs
            .GroupBy(log => log.BusinessStatus)
            .OrderBy(group => group.Key)
            .Select(group => new StatusBreakdownItem(group.Key, group.Count()))
            .ToList();

        var errorTypeBreakdown = logs
            .Where(log => log.BusinessStatus != RequestBusinessStatus.Success)
            .GroupBy(log => string.IsNullOrWhiteSpace(log.ErrorType) ? "Unknown" : log.ErrorType.Trim())
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(10)
            .Select(group => new ErrorTypeBreakdownItem(group.Key, group.Count()))
            .ToList();

        var accountBreakdown = logs
            .GroupBy(log => log.UpstreamAccountId)
            .Select(group => new AccountBreakdownItem(
                group.Key,
                ResolveAccountName(group.Key, accountNames),
                group.Count(),
                group.Count(log => log.BusinessStatus == RequestBusinessStatus.Success),
                group.Count(log => log.BusinessStatus != RequestBusinessStatus.Success)))
            .OrderByDescending(item => item.RequestCount)
            .ThenBy(item => item.Name)
            .Take(10)
            .ToList();

        var keyBreakdown = logs
            .GroupBy(log => log.ClientKeyId)
            .Select(group => new KeyBreakdownItem(
                group.Key,
                ResolveClientKeyName(group.Key, clientKeyNames),
                group.Count(),
                group.Count(log => log.BusinessStatus == RequestBusinessStatus.Success),
                group.Count(log => log.BusinessStatus != RequestBusinessStatus.Success)))
            .OrderByDescending(item => item.RequestCount)
            .ThenBy(item => item.Name)
            .Take(10)
            .ToList();

        return new RequestStatsResponse(
            range.Range,
            range.BucketUnit,
            buckets,
            statusBreakdown,
            errorTypeBreakdown,
            accountBreakdown,
            keyBreakdown);
    }

    private static string NormalizeRange(string? range) => range?.Trim().ToLowerInvariant() switch
    {
        null or "" => "today",
        "today" => "today",
        "7day" or "7days" or "7d" => "7d",
        "30day" or "30days" or "30d" => "30d",
        var value => value
    };

    private static RequestStatsBucket BuildBucket(RequestStatsRange range, DateTime bucketStartUtc, IEnumerable<RequestLog> source)
    {
        var bucketLogs = source.ToList();
        var requestCount = bucketLogs.Count;
        var successCount = bucketLogs.Count(log => log.BusinessStatus == RequestBusinessStatus.Success);
        var failureCount = requestCount - successCount;

        return new RequestStatsBucket(
            bucketStartUtc,
            FormatLabel(range.BucketUnit, bucketStartUtc),
            requestCount,
            successCount,
            failureCount,
            requestCount == 0 ? null : (double)successCount / requestCount,
            bucketLogs.Where(log => log.DurationMs is not null).Average(log => (double?)log.DurationMs) ?? 0,
            bucketLogs.Sum(log => log.InputTokens ?? 0),
            bucketLogs.Sum(log => log.OutputTokens ?? 0),
            bucketLogs.Sum(log => log.TotalTokens ?? 0),
            bucketLogs.Sum(log => log.ImageTotalTokens ?? 0));
    }

    private static IEnumerable<DateTime> CreateBuckets(RequestStatsRange range)
    {
        for (var i = 0; i < range.BucketCount; i++)
        {
            yield return range.BucketUnit == "hour"
                ? range.StartUtc.AddHours(i)
                : range.StartUtc.AddDays(i);
        }
    }

    private static bool IsInBucket(RequestStatsRange range, DateTime startedAt, DateTime bucketStartUtc)
    {
        var bucketEndUtc = range.BucketUnit == "hour" ? bucketStartUtc.AddHours(1) : bucketStartUtc.AddDays(1);
        return startedAt >= bucketStartUtc && startedAt < bucketEndUtc;
    }

    private static string FormatLabel(string bucketUnit, DateTime bucketStartUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(bucketStartUtc, DateTimeKind.Utc), ChinaTimeZone);
        return bucketUnit == "hour" ? local.ToString("HH:00") : local.ToString("MM-dd");
    }

    private static string ResolveAccountName(Guid? accountId, IReadOnlyDictionary<Guid, string> accountNames)
    {
        if (accountId is null) return "未分配账号";
        return accountNames.TryGetValue(accountId.Value, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : accountId.Value.ToString("N")[..8];
    }

    private static string ResolveClientKeyName(Guid? clientKeyId, IReadOnlyDictionary<Guid, string> clientKeyNames)
    {
        if (clientKeyId is null) return "未识别 Key";
        return clientKeyNames.TryGetValue(clientKeyId.Value, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : clientKeyId.Value.ToString("N")[..8];
    }

    private static DateTime ToUtc(DateTime localDateTime) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), ChinaTimeZone);

    private static TimeZoneInfo ResolveChinaTimeZone()
    {
        foreach (var id in new[] { "Asia/Shanghai", "China Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
