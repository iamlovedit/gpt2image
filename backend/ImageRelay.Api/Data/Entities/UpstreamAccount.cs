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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
