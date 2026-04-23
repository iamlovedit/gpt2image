using ImageRelay.Api.Data;
using ImageRelay.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImageRelay.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboard(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/dashboard").RequireAuthorization();

        g.MapGet("/summary", async (AppDbContext db) =>
        {
            var since = DateTime.UtcNow.AddHours(-24);

            var total24h = await db.RequestLogs.CountAsync(l => l.StartedAt >= since);
            var success24h = await db.RequestLogs.CountAsync(l => l.StartedAt >= since && l.BusinessStatus == RequestBusinessStatus.Success);
            var avgDurationMs = await db.RequestLogs
                .Where(l => l.StartedAt >= since && l.DurationMs != null)
                .AverageAsync(l => (double?)l.DurationMs) ?? 0;

            var accountTotal = await db.UpstreamAccounts.CountAsync();
            var accountHealthy = await db.UpstreamAccounts.CountAsync(a => a.Status == UpstreamAccountStatus.Healthy);
            var accountByStatus = await db.UpstreamAccounts
                .GroupBy(a => a.Status)
                .Select(x => new { status = x.Key, count = x.Count() })
                .ToListAsync();

            var keysActive = await db.ClientApiKeys.CountAsync(k => k.Status == ClientApiKeyStatus.Active);
            var keysTotal = await db.ClientApiKeys.CountAsync();

            var recent = await db.RequestLogs.AsNoTracking()
                .OrderByDescending(l => l.StartedAt)
                .Take(20)
                .ToListAsync();

            return Results.Ok(new
            {
                total24h,
                success24h,
                successRate = total24h == 0 ? (double?)null : (double)success24h / total24h,
                avgDurationMs,
                accountTotal,
                accountHealthy,
                accountByStatus,
                keysActive,
                keysTotal,
                recent
            });
        });
    }
}
