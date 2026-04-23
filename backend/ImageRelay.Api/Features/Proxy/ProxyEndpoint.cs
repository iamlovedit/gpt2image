using ImageRelay.Api.Data;
using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Middleware;
using ImageRelay.Api.Services;
using System.Text.Json;

namespace ImageRelay.Api.Features.Proxy;

public static class ProxyEndpoint
{
    public static void MapProxy(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/v1/responses", ProxyResponses).AllowAnonymous();
    }

    private static async Task ProxyResponses(HttpContext ctx)
    {
        var services = ctx.RequestServices;
        var db = services.GetRequiredService<AppDbContext>();
        var keyGen = services.GetRequiredService<ApiKeyGenerator>();
        var rateLimiter = services.GetRequiredService<ClientRateLimiter>();
        var selector = services.GetRequiredService<AccountSelector>();
        var forwarder = services.GetRequiredService<UpstreamForwarder>();
        var ct = ctx.RequestAborted;

        var clientKey = await ClientApiKeyAuth.ResolveAsync(ctx, db, keyGen);
        if (clientKey is null)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = new { message = "invalid api key" } }), ct);
            return;
        }

        var lease = rateLimiter.TryAcquire(clientKey);
        if (!lease.Ok)
        {
            ctx.Response.StatusCode = 429;
            ctx.Response.ContentType = "application/json";
            ctx.Response.Headers["Retry-After"] = "5";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = new { message = $"rate limited: {lease.Reason.ToString().ToLowerInvariant()}" }
            }), ct);
            return;
        }

        using (lease.Release)
        {
            await forwarder.ForwardAsync(ctx, clientKey, selector, db, ct);
        }
    }
}
