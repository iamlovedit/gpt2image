namespace ImageRelay.Api.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IApiKeyGenerator
{
    GeneratedApiKey Generate();
    string HashKey(string plaintext);
}

public interface IJwtTokenService
{
    string Issue(AdminUser user);
}

public interface IClientRateLimiter
{
    RateLimitLease TryAcquire(ClientApiKey key);
}

public interface ITokenRefresher
{
    Task EnsureFreshAsync(UpstreamAccount account, bool force, CancellationToken ct);
}

public interface IUpstreamForwarder
{
    Task ForwardAsync(
        HttpContext ctx,
        ClientApiKey clientKey,
        IAccountSelector selector,
        AppDbContext db,
        CancellationToken ct);
}

public interface IAccountConnectivityStatusUpdater
{
    void MarkSuccess(UpstreamAccount account);
    void MarkRefreshFailure(UpstreamAccount account, string error);
    void MarkNetworkFailure(UpstreamAccount account, string error);
    void MarkHttpFailure(UpstreamAccount account, int statusCode, string body);
}

public interface IAccountConnectivityTester
{
    Task<AccountConnectivityTestResult> TestAsync(AppDbContext db, UpstreamAccount account, CancellationToken ct);
}

public interface IAccountSelector
{
    Task<AccountLease?> PickAsync(HashSet<Guid> excluded, CancellationToken ct);
}

public interface IAccountConcurrencyRegistry
{
    IDisposable? TryAcquire(Guid accountId, int limit);
}
