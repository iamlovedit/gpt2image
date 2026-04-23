namespace ImageRelay.Api.Data.Entities;

public class ModelMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalName { get; set; } = string.Empty;
    public string UpstreamName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
