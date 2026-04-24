namespace ImageRelay.Api.Features.Dashboard;

public sealed record RequestStatsResponse(
    string Range,
    string BucketUnit,
    IReadOnlyList<RequestStatsBucket> Series,
    IReadOnlyList<StatusBreakdownItem> StatusBreakdown,
    IReadOnlyList<ErrorTypeBreakdownItem> ErrorTypeBreakdown,
    IReadOnlyList<AccountBreakdownItem> AccountBreakdown,
    IReadOnlyList<KeyBreakdownItem> KeyBreakdown);

public sealed record RequestStatsBucket(
    DateTime BucketStart,
    string Label,
    int RequestCount,
    int SuccessCount,
    int FailureCount,
    double? SuccessRate,
    double AvgDurationMs,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    long ImageTotalTokens);

public sealed record StatusBreakdownItem(RequestBusinessStatus Status, int Count);

public sealed record ErrorTypeBreakdownItem(string ErrorType, int Count);

public sealed record AccountBreakdownItem(Guid? AccountId, string Name, int RequestCount, int SuccessCount, int FailureCount);

public sealed record KeyBreakdownItem(Guid? ClientKeyId, string Name, int RequestCount, int SuccessCount, int FailureCount);

public sealed record RequestStatsRange(string Range, string BucketUnit, DateTime StartUtc, DateTime EndUtc, int BucketCount);
