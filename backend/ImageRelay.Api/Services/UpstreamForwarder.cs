using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ImageRelay.Api.Configuration;
using ImageRelay.Api.Data;
using ImageRelay.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ImageRelay.Api.Services;

public class UpstreamForwarder(
    IHttpClientFactory httpFactory,
    IOptions<UpstreamOptions> upstream,
    IOptions<ProxyOptions> proxy,
    TokenRefresher refresher,
    ILogger<UpstreamForwarder> logger)
{
    public async Task ForwardAsync(
        HttpContext ctx,
        ClientApiKey clientKey,
        AccountSelector selector,
        AppDbContext db,
        CancellationToken ct)
    {
        var requestId = ctx.TraceIdentifier;
        var sw = Stopwatch.StartNew();

        var log = new RequestLog
        {
            RequestId = requestId,
            ClientKeyId = clientKey.Id,
            StartedAt = DateTime.UtcNow,
            BusinessStatus = RequestBusinessStatus.InternalError
        };

        string? rewrittenBody = null;
        string? externalModel = null;
        string? upstreamModel = null;
        UpstreamHeaderSettings? headerSettings = null;

        try
        {
            // Read and rewrite body
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                log.BusinessStatus = RequestBusinessStatus.ClientError;
                log.ErrorType = "EmptyBody";
                await WriteJsonError(ctx, 400, "empty request body");
                return;
            }

            using (var doc = TryParse(rawBody, out var parseErr))
            {
                if (doc is null)
                {
                    log.BusinessStatus = RequestBusinessStatus.ClientError;
                    log.ErrorType = "InvalidJson";
                    log.ErrorMessage = parseErr;
                    await WriteJsonError(ctx, 400, "invalid JSON body");
                    return;
                }

                if (!doc.RootElement.TryGetProperty("model", out var modelEl) || modelEl.ValueKind != JsonValueKind.String)
                {
                    log.BusinessStatus = RequestBusinessStatus.ClientError;
                    log.ErrorType = "MissingModel";
                    await WriteJsonError(ctx, 400, "missing model field");
                    return;
                }
                externalModel = modelEl.GetString();
                log.ExternalModel = externalModel;
            }

            var mapping = await db.ModelMappings.AsNoTracking()
                .FirstOrDefaultAsync(m => m.ExternalName == externalModel && m.IsEnabled, ct);
            if (mapping is null)
            {
                log.BusinessStatus = RequestBusinessStatus.ClientError;
                log.ErrorType = "ModelNotAllowed";
                await WriteJsonError(ctx, 400, $"model '{externalModel}' is not in mapping whitelist");
                return;
            }
            upstreamModel = mapping.UpstreamName;
            log.UpstreamModel = upstreamModel;

            rewrittenBody = RewriteModel(rawBody, upstreamModel);
            headerSettings = await db.UpstreamHeaderSettings.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == UpstreamHeaderSettings.SingletonId, ct)
                ?? UpstreamHeaderSettings.CreateDefault();

            var excluded = new HashSet<Guid>();
            var maxRetries = proxy.Value.MaxRetries;
            var attempt = 0;

            while (attempt <= maxRetries)
            {
                attempt++;
                log.RetryCount = attempt - 1;
                var lease = await selector.PickAsync(excluded, ct);
                if (lease is null)
                {
                    log.BusinessStatus = RequestBusinessStatus.NoAvailableAccount;
                    log.ErrorType = "NoHealthyAccount";
                    await WriteJsonError(ctx, 503, "no available upstream account");
                    return;
                }

                using (lease.ConcurrencyRelease)
                {
                    var account = lease.Account;
                    log.UpstreamAccountId = account.Id;

                    var outcome = await AttemptAccount(ctx, db, account, headerSettings, rewrittenBody!, log, ct);
                    if (outcome == AttemptOutcome.Success) return;
                    excluded.Add(account.Id);
                }
            }

            log.BusinessStatus = RequestBusinessStatus.UpstreamError;
            log.ErrorType ??= "ExhaustedRetries";
            await WriteJsonError(ctx, 502, "all upstream attempts failed");
        }
        catch (OperationCanceledException)
        {
            log.ErrorType = "Cancelled";
            log.BusinessStatus = RequestBusinessStatus.InternalError;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled proxy error for request {RequestId}", requestId);
            log.ErrorType = ex.GetType().Name;
            log.ErrorMessage = Truncate(ex.Message, 1024);
            log.BusinessStatus = RequestBusinessStatus.InternalError;
            if (!ctx.Response.HasStarted)
                await WriteJsonError(ctx, 500, "internal proxy error");
        }
        finally
        {
            sw.Stop();
            log.CompletedAt = DateTime.UtcNow;
            log.DurationMs = sw.ElapsedMilliseconds;
            try
            {
                db.RequestLogs.Add(log);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist request log {RequestId}", requestId);
            }

            logger.LogInformation(
                "proxy {RequestId} key={ClientKey} account={Account} model={External}->{Upstream} status={Status} retries={Retry} dur={Duration}ms",
                requestId, clientKey.Id, log.UpstreamAccountId, externalModel, upstreamModel,
                log.BusinessStatus, log.RetryCount, log.DurationMs);
        }
    }

    private enum AttemptOutcome { Success, RetryNextAccount }

    private async Task<AttemptOutcome> AttemptAccount(
        HttpContext ctx, AppDbContext db, UpstreamAccount account,
        UpstreamHeaderSettings headerSettings,
        string rewrittenBody, RequestLog log, CancellationToken ct)
    {
        var triedRefresh = false;

        while (true)
        {
            try { await refresher.EnsureFreshAsync(account, force: false, ct); }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Proactive refresh failed for account {Id}", account.Id);
                return AttemptOutcome.RetryNextAccount;
            }

            using var client = httpFactory.CreateClient("upstream");
            using var req = BuildUpstreamRequest(account, headerSettings, rewrittenBody);

            HttpResponseMessage resp;
            try
            {
                resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Upstream request failed for account {Id}", account.Id);
                await MarkFailure(db, account, "NetworkError: " + Truncate(ex.Message, 256), ct);
                return AttemptOutcome.RetryNextAccount;
            }

            var status = resp.StatusCode;
            log.HttpStatus = (int)status;

            if (status == HttpStatusCode.OK)
            {
                ctx.Response.StatusCode = 200;
                CopyStreamHeaders(resp, ctx.Response);
                ctx.Response.Headers["X-Accel-Buffering"] = "no";

                var eventCount = 0;
                await using (var upstreamStream = await resp.Content.ReadAsStreamAsync(ct))
                {
                    var buffer = new byte[16 * 1024];
                    int read;
                    while ((read = await upstreamStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await ctx.Response.Body.WriteAsync(buffer.AsMemory(0, read), ct);
                        await ctx.Response.Body.FlushAsync(ct);
                        eventCount += CountEvents(buffer.AsSpan(0, read));
                    }
                }
                resp.Dispose();

                log.SseEventCount = eventCount;
                log.BusinessStatus = RequestBusinessStatus.Success;
                log.HttpStatus = 200;
                account.SuccessCount++;
                account.LastError = null;
                await db.SaveChangesAsync(CancellationToken.None);
                return AttemptOutcome.Success;
            }

            var errBody = await SafeReadBody(resp, ct);
            resp.Dispose();

            if (status == HttpStatusCode.Unauthorized)
            {
                if (!triedRefresh)
                {
                    triedRefresh = true;
                    try { await refresher.EnsureFreshAsync(account, force: true, ct); }
                    catch { return AttemptOutcome.RetryNextAccount; }
                    continue;
                }
                await MarkInvalid(db, account, "401 after refresh: " + Truncate(errBody, 256), ct);
                return AttemptOutcome.RetryNextAccount;
            }

            if ((int)status == 429)
            {
                await MarkCooling(db, account, proxy.Value.CoolingMinutes, Truncate(errBody, 256), ct);
                return AttemptOutcome.RetryNextAccount;
            }

            if (status is HttpStatusCode.Forbidden or HttpStatusCode.PaymentRequired)
            {
                await MarkBanned(db, account, $"{(int)status}: " + Truncate(errBody, 256), ct);
                return AttemptOutcome.RetryNextAccount;
            }

            await MarkFailure(db, account, $"HTTP {(int)status}: " + Truncate(errBody, 256), ct);
            log.ErrorType = "UpstreamError";
            log.ErrorMessage = Truncate(errBody, 1024);
            return AttemptOutcome.RetryNextAccount;
        }
    }

    private static JsonDocument? TryParse(string raw, out string? error)
    {
        try { error = null; return JsonDocument.Parse(raw); }
        catch (JsonException ex) { error = ex.Message; return null; }
    }

    private HttpRequestMessage BuildUpstreamRequest(
        UpstreamAccount account,
        UpstreamHeaderSettings headerSettings,
        string body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, upstream.Value.ResponsesUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        req.Headers.Accept.ParseAdd("text/event-stream");
        req.Headers.TryAddWithoutValidation("oai-language", "en-US");
        req.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");
        req.Headers.TryAddWithoutValidation("user-agent", ResolveRequired(headerSettings.UserAgent, UpstreamHeaderSettings.DefaultUserAgent));
        req.Headers.TryAddWithoutValidation("version", ResolveRequired(headerSettings.Version, UpstreamHeaderSettings.DefaultVersion));
        req.Headers.TryAddWithoutValidation("originator", ResolveRequired(headerSettings.Originator, UpstreamHeaderSettings.DefaultOriginator));

        if (!string.IsNullOrWhiteSpace(account.ChatGptAccountId))
            req.Headers.TryAddWithoutValidation("chatgpt-account-id", account.ChatGptAccountId);

        if (!string.IsNullOrWhiteSpace(headerSettings.SessionId))
            req.Headers.TryAddWithoutValidation("session_id", headerSettings.SessionId);

        return req;
    }

    private static void CopyStreamHeaders(HttpResponseMessage resp, HttpResponse target)
    {
        var ct = resp.Content.Headers.ContentType?.ToString() ?? "text/event-stream; charset=utf-8";
        target.Headers["Content-Type"] = ct;
        target.Headers["Cache-Control"] = "no-cache";
        target.Headers["Connection"] = "keep-alive";
    }

    private static int CountEvents(ReadOnlySpan<byte> chunk)
    {
        // Count occurrences of "event:" at line starts (cheap heuristic).
        int count = 0;
        for (int i = 0; i < chunk.Length - 6; i++)
        {
            if ((i == 0 || chunk[i - 1] == (byte)'\n')
                && chunk[i] == (byte)'e' && chunk[i + 1] == (byte)'v' && chunk[i + 2] == (byte)'e'
                && chunk[i + 3] == (byte)'n' && chunk[i + 4] == (byte)'t' && chunk[i + 5] == (byte)':')
            {
                count++;
            }
        }
        return count;
    }

    private static string RewriteModel(string body, string upstreamModel)
    {
        using var doc = JsonDocument.Parse(body);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("model"))
                {
                    writer.WriteString("model", upstreamModel);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static async Task WriteJsonError(HttpContext ctx, int status, string message)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = new { message } }));
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return await resp.Content.ReadAsStringAsync(ct); }
        catch { return ""; }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static string ResolveRequired(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static async Task MarkFailure(AppDbContext db, UpstreamAccount a, string err, CancellationToken ct)
    {
        a.LastError = err;
        a.FailureCount++;
        await db.SaveChangesAsync(ct);
    }

    private static async Task MarkInvalid(AppDbContext db, UpstreamAccount a, string err, CancellationToken ct)
    {
        a.Status = UpstreamAccountStatus.Invalid;
        a.LastError = err;
        a.FailureCount++;
        await db.SaveChangesAsync(ct);
    }

    private static async Task MarkCooling(AppDbContext db, UpstreamAccount a, int minutes, string err, CancellationToken ct)
    {
        a.Status = UpstreamAccountStatus.Cooling;
        a.CoolingUntil = DateTime.UtcNow.AddMinutes(minutes);
        a.LastError = err;
        a.FailureCount++;
        await db.SaveChangesAsync(ct);
    }

    private static async Task MarkBanned(AppDbContext db, UpstreamAccount a, string err, CancellationToken ct)
    {
        a.Status = UpstreamAccountStatus.Banned;
        a.LastError = err;
        a.FailureCount++;
        await db.SaveChangesAsync(ct);
    }
}
