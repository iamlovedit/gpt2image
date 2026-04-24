namespace ImageRelay.Api.Data.Entities;

public class UpstreamAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string? ChatGptAccountId { get; set; }
    public DateTime? AccessTokenExpiresAt { get; set; }
    public UpstreamAccountStatus Status { get; set; } = UpstreamAccountStatus.Healthy;
    public DateTime? CoolingUntil { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public int ConcurrencyLimit { get; set; } = 2;
    public string? Notes { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Platform { get; set; }
    public string? AccountType { get; set; }
    public string? ProxyKey { get; set; }
    public int? Priority { get; set; }
    public decimal? RateMultiplier { get; set; }
    public bool? AutoPauseOnExpired { get; set; }
    public string? ChatGptUserId { get; set; }
    public string? ClientId { get; set; }
    public string? OrganizationId { get; set; }
    public string? PlanType { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
    public string? RawMetadataJson { get; set; }
    public int? CodexPrimaryUsedPercent { get; set; }
    public int? CodexSecondaryUsedPercent { get; set; }
    public int? CodexPrimaryWindowMinutes { get; set; }
    public int? CodexSecondaryWindowMinutes { get; set; }
    public int? CodexPrimaryResetAfterSeconds { get; set; }
    public int? CodexSecondaryResetAfterSeconds { get; set; }
    public DateTime? CodexPrimaryResetAt { get; set; }
    public DateTime? CodexSecondaryResetAt { get; set; }
    public int? CodexPrimaryOverSecondaryLimitPercent { get; set; }
    public DateTime? CodexRateLimitUpdatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
