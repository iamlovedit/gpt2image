using ImageRelay.Api.Data.Entities;

namespace ImageRelay.Api.Features.Logs;

public sealed record RequestLogDto(
    Guid Id,
    string RequestId,
    Guid? ClientKeyId,
    string? ClientKeyName,
    Guid? UpstreamAccountId,
    string? ExternalModel,
    string? UpstreamModel,
    DateTime StartedAt,
    DateTime? CompletedAt,
    long? DurationMs,
    int? HttpStatus,
    RequestBusinessStatus BusinessStatus,
    string? ErrorType,
    string? ErrorMessage,
    int SseEventCount,
    int RetryCount,
    long? InputTokens,
    long? OutputTokens,
    long? TotalTokens,
    long? ImageInputTokens,
    long? ImageOutputTokens,
    long? ImageTotalTokens)
{
    public static RequestLogDto From(RequestLog log, string? clientKeyName) => new(
        log.Id,
        log.RequestId,
        log.ClientKeyId,
        clientKeyName,
        log.UpstreamAccountId,
        log.ExternalModel,
        log.UpstreamModel,
        log.StartedAt,
        log.CompletedAt,
        log.DurationMs,
        log.HttpStatus,
        log.BusinessStatus,
        log.ErrorType,
        log.ErrorMessage,
        log.SseEventCount,
        log.RetryCount,
        log.InputTokens,
        log.OutputTokens,
        log.TotalTokens,
        log.ImageInputTokens,
        log.ImageOutputTokens,
        log.ImageTotalTokens);
}

