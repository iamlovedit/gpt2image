namespace ImageRelay.Api.Data.Entities;

public class UpstreamHeaderSettings
{
    public const int SingletonId = 1;
    public const string DefaultUserAgent = "ImageRelay/1.0";
    public const string DefaultVersion = "1.0.0";
    public const string DefaultOriginator = "image-relay";

    public int Id { get; set; } = SingletonId;
    public string UserAgent { get; set; } = DefaultUserAgent;
    public string Version { get; set; } = DefaultVersion;
    public string Originator { get; set; } = DefaultOriginator;
    public string? SessionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static UpstreamHeaderSettings CreateDefault() => new()
    {
        Id = SingletonId,
        UserAgent = DefaultUserAgent,
        Version = DefaultVersion,
        Originator = DefaultOriginator
    };
}
