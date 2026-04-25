using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace ImageRelay.Api.Services;

public class AccountConnectivityTester(
    IHttpClientFactory httpFactory,
    IOptions<UpstreamOptions> upstream,
    ITokenRefresher refresher,
    IAccountConnectivityStatusUpdater statusUpdater) : IAccountConnectivityTester
{
    public const string ConnectivityExternalModel = "gpt-5.4";
    public const string ConnectivityTestInput = "hi";
    public const string ConnectivityTestInstructions = "you are a helpful assistant";

    public async Task<AccountConnectivityTestResult> TestAsync(AppDbContext db, UpstreamAccount account, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await refresher.EnsureFreshAsync(account, force: false, ct);
        }
        catch (Exception ex)
        {
            statusUpdater.MarkRefreshFailure(account, ex.Message);
            await db.SaveChangesAsync(ct);
            return new AccountConnectivityTestResult(false, null, account.LastError ?? "refresh failed", sw.ElapsedMilliseconds, account.Status);
        }

        var headers = await db.UpstreamHeaderSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == UpstreamHeaderSettings.SingletonId, ct)
            ?? UpstreamHeaderSettings.CreateDefault();
        var upstreamModel = await db.ModelMappings.AsNoTracking()
            .Where(m => m.ExternalName == ConnectivityExternalModel && m.IsEnabled)
            .Select(m => m.UpstreamName)
            .FirstOrDefaultAsync(ct)
            ?? ConnectivityExternalModel;
        var triedRefresh = false;
        while (true)
        {
            using var client = httpFactory.CreateClient("upstream");
            using var req = BuildRequest(account, headers, upstream.Value.ResponsesUrl, upstreamModel);

            HttpResponseMessage resp;
            try
            {
                resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex)
            {
                statusUpdater.MarkNetworkFailure(account, ex.Message);
                await db.SaveChangesAsync(ct);
                return new AccountConnectivityTestResult(false, null, account.LastError ?? ex.Message, sw.ElapsedMilliseconds, account.Status);
            }

            using var _ = resp;
            var statusCode = (int)resp.StatusCode;
            var body = await SafeReadBody(resp, ct);

            if (resp.IsSuccessStatusCode)
            {
                statusUpdater.MarkSuccess(account);
                await db.SaveChangesAsync(ct);
                return new AccountConnectivityTestResult(true, statusCode, "OpenAI account connectivity test passed", sw.ElapsedMilliseconds, account.Status);
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized && !triedRefresh)
            {
                triedRefresh = true;
                try
                {
                    await refresher.EnsureFreshAsync(account, force: true, ct);
                    continue;
                }
                catch (Exception ex)
                {
                    statusUpdater.MarkRefreshFailure(account, ex.Message);
                    await db.SaveChangesAsync(ct);
                    return new AccountConnectivityTestResult(false, statusCode, account.LastError ?? "refresh failed", sw.ElapsedMilliseconds, account.Status);
                }
            }

            statusUpdater.MarkHttpFailure(account, statusCode, body);
            await db.SaveChangesAsync(ct);
            return new AccountConnectivityTestResult(false, statusCode, account.LastError ?? $"HTTP {statusCode}", sw.ElapsedMilliseconds, account.Status);
        }
    }

    public static HttpRequestMessage BuildRequest(
        UpstreamAccount account,
        UpstreamHeaderSettings headers,
        string responsesUrl,
        string model)
    {
        var body = JsonSerializer.Serialize(BuildRequestBody(model));

        var req = new HttpRequestMessage(HttpMethod.Post, responsesUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        req.Headers.Accept.ParseAdd("text/event-stream");
        req.Headers.TryAddWithoutValidation("oai-language", "en-US");
        req.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");
        req.Headers.TryAddWithoutValidation("user-agent", ResolveRequired(headers.UserAgent, UpstreamHeaderSettings.DefaultUserAgent));
        req.Headers.TryAddWithoutValidation("version", ResolveRequired(headers.Version, UpstreamHeaderSettings.DefaultVersion));
        req.Headers.TryAddWithoutValidation("originator", ResolveRequired(headers.Originator, UpstreamHeaderSettings.DefaultOriginator));

        if (!string.IsNullOrWhiteSpace(account.ChatGptAccountId))
            req.Headers.TryAddWithoutValidation("chatgpt-account-id", account.ChatGptAccountId);
        if (!string.IsNullOrWhiteSpace(headers.SessionId))
            req.Headers.TryAddWithoutValidation("session_id", headers.SessionId);

        return req;
    }

    public static object BuildRequestBody(string model) => new
    {
        model,
        input = new[]
        {
            new
            {
                role = "user",
                content = ConnectivityTestInput
            }
        },
        instructions = ConnectivityTestInstructions,
        tool_choice = "auto",
        stream = true,
        store = false
    };

    private static string ResolveRequired(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static async Task<string> SafeReadBody(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var text = await resp.Content.ReadAsStringAsync(ct);
            return text.Length <= 1024 ? text : text[..1024];
        }
        catch
        {
            return "";
        }
    }
}
