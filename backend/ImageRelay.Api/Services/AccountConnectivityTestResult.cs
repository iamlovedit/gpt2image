using ImageRelay.Api.Data.Entities;

namespace ImageRelay.Api.Services;

public record AccountConnectivityTestResult(
    bool Ok,
    int? HttpStatus,
    string Message,
    long DurationMs,
    UpstreamAccountStatus Status);
