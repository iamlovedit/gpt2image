namespace ImageRelay.Api.Services;

public class AccountConnectivityStatusUpdater(IOptions<ProxyOptions> proxy) : IAccountConnectivityStatusUpdater
{
    public void MarkSuccess(UpstreamAccount account)
    {
        account.Status = UpstreamAccountStatus.Healthy;
        account.CoolingUntil = null;
        account.LastError = null;
        account.LastUsedAt = DateTime.UtcNow;
        account.SuccessCount++;
    }

    public void MarkRefreshFailure(UpstreamAccount account, string error)
    {
        account.Status = UpstreamAccountStatus.Invalid;
        account.LastError = "connectivity refresh failed: " + Truncate(error, 512);
        account.FailureCount++;
    }

    public void MarkNetworkFailure(UpstreamAccount account, string error)
    {
        account.LastError = "Connectivity NetworkError: " + Truncate(error, 512);
        account.FailureCount++;
    }

    public void MarkHttpFailure(UpstreamAccount account, int statusCode, string body)
    {
        var detail = $"connectivity HTTP {statusCode}: " + Truncate(body, 512);
        account.LastError = detail;
        account.FailureCount++;

        if (statusCode == StatusCodes.Status429TooManyRequests)
        {
            account.Status = UpstreamAccountStatus.Cooling;
            account.CoolingUntil = DateTime.UtcNow.AddMinutes(Math.Max(1, proxy.Value.CoolingMinutes));
        }
        else if (statusCode is StatusCodes.Status402PaymentRequired or StatusCodes.Status403Forbidden)
        {
            account.Status = UpstreamAccountStatus.Banned;
        }
        else if (statusCode == StatusCodes.Status401Unauthorized)
        {
            account.Status = UpstreamAccountStatus.Invalid;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
