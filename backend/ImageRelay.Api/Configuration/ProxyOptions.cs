namespace ImageRelay.Api.Configuration;

public class ProxyOptions
{
    public const string SectionName = "Proxy";

    public int MaxRetries { get; set; } = 2;
    public int CoolingMinutes { get; set; } = 5;
    public int AccountConcurrency { get; set; } = 2;
    public int RefreshSkewSeconds { get; set; } = 300;
}
