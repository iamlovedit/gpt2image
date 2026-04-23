namespace ImageRelay.Api.Data.Entities;

public enum UpstreamAccountStatus
{
    Healthy = 0,
    Cooling = 1,
    RateLimited = 2,
    Banned = 3,
    Invalid = 4,
    Disabled = 5
}
