namespace ImageRelay.Api.Data.Entities;

public enum RequestBusinessStatus
{
    Success = 0,
    UpstreamError = 1,
    AuthFailed = 2,
    RateLimited = 3,
    NoAvailableAccount = 4,
    ClientError = 5,
    InternalError = 6
}

public class RequestLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestId { get; set; } = string.Empty;
    public Guid? ClientKeyId { get; set; }
    public Guid? UpstreamAccountId { get; set; }
    public string? ExternalModel { get; set; }
    public string? UpstreamModel { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public int? HttpStatus { get; set; }
    public RequestBusinessStatus BusinessStatus { get; set; } = RequestBusinessStatus.InternalError;
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public int SseEventCount { get; set; }
    public int RetryCount { get; set; }
}
