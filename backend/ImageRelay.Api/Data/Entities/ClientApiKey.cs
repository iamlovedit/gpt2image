namespace ImageRelay.Api.Data.Entities;

public class ClientApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public ClientApiKeyStatus Status { get; set; } = ClientApiKeyStatus.Active;
    public DateTime? ExpiresAt { get; set; }
    public int RpmLimit { get; set; } = 60;
    public int ConcurrencyLimit { get; set; } = 4;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
