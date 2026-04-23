using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ImageRelay.Api.Configuration;
using ImageRelay.Api.Data;
using ImageRelay.Api.Data.Entities;
using Microsoft.Extensions.Options;

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

            var client = httpFactory.CreateClient("upstream");
            var payload = new
            {
                grant_type = "refresh_token",
                client_id = upstream.Value.TokenClientId,
                refresh_token = fresh.RefreshToken,
                redirect_uri = "com.openai.chat://auth0.openai.com/ios/com.openai.chat/callback",
                scope = "openid profile email offline_access"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, upstream.Value.TokenUrl)
            {
                Content = JsonContent.Create(payload)
            };
            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                fresh.Status = UpstreamAccountStatus.Invalid;
                fresh.LastError = $"refresh failed: HTTP {(int)resp.StatusCode} {Truncate(body, 512)}";
                await db.SaveChangesAsync(ct);
                logger.LogWarning("Refresh failed for account {Id}: {Status}", fresh.Id, resp.StatusCode);
                throw new HttpRequestException($"token refresh failed: {resp.StatusCode}");
            }

            var parsed = System.Text.Json.JsonSerializer.Deserialize<RefreshResponse>(body,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.AccessToken))
            {
                fresh.Status = UpstreamAccountStatus.Invalid;
                fresh.LastError = "refresh response missing access_token";
                await db.SaveChangesAsync(ct);
                throw new InvalidOperationException("refresh response missing access_token");
            }

            fresh.AccessToken = parsed.AccessToken;
            if (!string.IsNullOrWhiteSpace(parsed.RefreshToken)) fresh.RefreshToken = parsed.RefreshToken!;
            fresh.AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(parsed.ExpiresIn > 0 ? parsed.ExpiresIn : 3600);
            fresh.LastError = null;
            if (fresh.Status == UpstreamAccountStatus.Invalid)
                fresh.Status = UpstreamAccountStatus.Healthy;
            await db.SaveChangesAsync(ct);

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

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private class RefreshResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    }
}
