using System.Collections.Concurrent;
using System.Net;
using System.Text.Json.Serialization;

namespace ImageRelay.Api.Services;

public class TokenRefresher(
    IHttpClientFactory httpFactory,
    IOptions<UpstreamOptions> upstream,
    IOptions<ProxyOptions> proxy,
    IServiceScopeFactory scopeFactory,
    ILogger<TokenRefresher> logger)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    public async Task EnsureFreshAsync(UpstreamAccount account, bool force, CancellationToken ct)
    {
        var skew = TimeSpan.FromSeconds(proxy.Value.RefreshSkewSeconds);
        if (!force && account.AccessTokenExpiresAt is DateTime exp
                   && exp - DateTime.UtcNow > skew)
        {
            return;
        }

        var sem = Locks.GetOrAdd(account.Id, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            // Re-read from DB to see if another process already refreshed.
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fresh = await db.UpstreamAccounts.FindAsync([account.Id], ct);
            if (fresh is null) throw new InvalidOperationException("Account disappeared during refresh.");

            if (!force && fresh.AccessTokenExpiresAt is DateTime exp2 && exp2 - DateTime.UtcNow > skew)
            {
                account.AccessToken = fresh.AccessToken;
                account.AccessTokenExpiresAt = fresh.AccessTokenExpiresAt;
                return;
            }

            var clientId = NormalizeClientId(fresh.ClientId);
            if (clientId is null)
            {
                MarkPermanentRefreshFailure(fresh, "refresh failed: missing client_id");
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("refresh failed: missing client_id");
            }

            HttpResponseMessage resp;
            string body;
            try
            {
                var headerSettings = await db.UpstreamHeaderSettings.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == UpstreamHeaderSettings.SingletonId, ct)
                    ?? UpstreamHeaderSettings.CreateDefault();

                var client = httpFactory.CreateClient("upstream");
                using var req = BuildRefreshRequest(upstream.Value.TokenUrl, clientId, fresh.RefreshToken, headerSettings);
                resp = await client.SendAsync(req, ct);
                body = await resp.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                MarkTemporaryRefreshFailure(fresh, "refresh failed: timeout", proxy.Value.CoolingMinutes);
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Refresh timed out for account {Id}", fresh.Id);
                throw;
            }
            catch (HttpRequestException ex)
            {
                MarkTemporaryRefreshFailure(fresh, "refresh failed: network error " + Truncate(ex.Message, 512), proxy.Value.CoolingMinutes);
                await db.SaveChangesAsync(ct);
                logger.LogWarning(ex, "Refresh network error for account {Id}", fresh.Id);
                throw;
            }

            using (resp)
            {
                if (!resp.IsSuccessStatusCode)
                {
                    MarkHttpRefreshFailure(fresh, resp.StatusCode, body, proxy.Value.CoolingMinutes);
                    await db.SaveChangesAsync(ct);
                    logger.LogWarning("Refresh failed for account {Id}: {Status}", fresh.Id, resp.StatusCode);
                    throw new HttpRequestException($"token refresh failed: {resp.StatusCode}");
                }

                var parsed = JsonSerializer.Deserialize<RefreshResponse>(body,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (parsed is null || string.IsNullOrWhiteSpace(parsed.AccessToken))
                {
                    MarkPermanentRefreshFailure(fresh, "refresh response missing access_token");
                    await db.SaveChangesAsync(ct);
                    throw new InvalidOperationException("refresh response missing access_token");
                }

                ApplySuccessfulRefresh(fresh, parsed.AccessToken, parsed.RefreshToken, parsed.ExpiresIn);
                await db.SaveChangesAsync(ct);
            }

            account.AccessToken = fresh.AccessToken;
            account.RefreshToken = fresh.RefreshToken;
            account.AccessTokenExpiresAt = fresh.AccessTokenExpiresAt;
            account.Status = fresh.Status;

            logger.LogInformation("Refreshed token for account {Id}", account.Id);
        }
        finally
        {
            sem.Release();
        }
    }

    internal static HttpRequestMessage BuildRefreshRequest(
        string tokenUrl,
        string clientId,
        string refreshToken,
        UpstreamHeaderSettings headerSettings)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["scope"] = "openid profile email offline_access"
        };

        var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        req.Headers.TryAddWithoutValidation(
            "user-agent",
            ResolveRequired(headerSettings.UserAgent, UpstreamHeaderSettings.DefaultUserAgent));
        return req;
    }

    private static string ResolveRequired(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    internal static string? NormalizeClientId(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();

    internal static void ApplySuccessfulRefresh(
        UpstreamAccount account,
        string accessToken,
        string? refreshToken,
        int expiresIn)
    {
        account.AccessToken = accessToken;
        if (!string.IsNullOrWhiteSpace(refreshToken)) account.RefreshToken = refreshToken!;
        account.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn > 0 ? expiresIn : 3600);
        account.LastError = null;
        account.CoolingUntil = null;
        if (account.Status is UpstreamAccountStatus.Invalid or UpstreamAccountStatus.Cooling)
            account.Status = UpstreamAccountStatus.Healthy;
    }

    internal static void MarkHttpRefreshFailure(
        UpstreamAccount account,
        HttpStatusCode statusCode,
        string body,
        int coolingMinutes)
    {
        var detail = $"refresh failed: HTTP {(int)statusCode} {Truncate(body, 512)}";
        if (IsPermanentRefreshFailure(statusCode))
            MarkPermanentRefreshFailure(account, detail);
        else
            MarkTemporaryRefreshFailure(account, detail, coolingMinutes);
    }

    private static bool IsPermanentRefreshFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    private static void MarkPermanentRefreshFailure(UpstreamAccount account, string error)
    {
        account.Status = UpstreamAccountStatus.Invalid;
        account.CoolingUntil = null;
        account.LastError = error;
        account.FailureCount++;
    }

    internal static void MarkTemporaryRefreshFailure(UpstreamAccount account, string error, int coolingMinutes)
    {
        account.Status = UpstreamAccountStatus.Cooling;
        account.CoolingUntil = DateTime.UtcNow.AddMinutes(Math.Max(1, coolingMinutes));
        account.LastError = error;
        account.FailureCount++;
    }

    private class RefreshResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    }
}
