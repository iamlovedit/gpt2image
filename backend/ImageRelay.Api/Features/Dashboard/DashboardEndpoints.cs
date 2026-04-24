using ImageRelay.Api.Features.Common;

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
            var inputTokens24h = await db.RequestLogs
                .Where(l => l.StartedAt >= since)
                .SumAsync(l => l.InputTokens ?? 0);
            var outputTokens24h = await db.RequestLogs
                .Where(l => l.StartedAt >= since)
                .SumAsync(l => l.OutputTokens ?? 0);
            var totalTokens24h = await db.RequestLogs
                .Where(l => l.StartedAt >= since)
                .SumAsync(l => l.TotalTokens ?? 0);
            var imageTotalTokens24h = await db.RequestLogs
                .Where(l => l.StartedAt >= since)
                .SumAsync(l => l.ImageTotalTokens ?? 0);

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

            return ApiResponse.Ok(new
            {
                total24h,
                success24h,
                successRate = total24h == 0 ? (double?)null : (double)success24h / total24h,
                avgDurationMs,
                inputTokens24h,
                outputTokens24h,
                totalTokens24h,
                imageTotalTokens24h,
                accountTotal,
                accountHealthy,
                accountByStatus,
                keysActive,
                keysTotal,
                recent
            });
        });

        g.MapGet("/request-stats", async (string? range, AppDbContext db) =>
        {
            var statsRange = RequestStatsService.CreateRange(range, DateTime.UtcNow);
            if (statsRange is null)
            {
                return ApiResponse.BadRequest("range must be one of: today, 7d, 30d");
            }

            var logs = await db.RequestLogs.AsNoTracking()
                .Where(l => l.StartedAt >= statsRange.StartUtc && l.StartedAt <= statsRange.EndUtc)
                .ToListAsync();

            var accountIds = logs
                .Select(l => l.UpstreamAccountId)
                .OfType<Guid>()
                .Distinct()
                .ToList();

            var accountNames = await db.UpstreamAccounts.AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => new { a.Id, Name = a.Name ?? a.Email ?? a.ChatGptAccountId })
                .ToDictionaryAsync(a => a.Id, a => a.Name ?? string.Empty);

            var clientKeyIds = logs
                .Select(l => l.ClientKeyId)
                .OfType<Guid>()
                .Distinct()
                .ToList();

            var clientKeyNames = await db.ClientApiKeys.AsNoTracking()
                .Where(k => clientKeyIds.Contains(k.Id))
                .ToDictionaryAsync(k => k.Id, k => k.Name);

            return ApiResponse.Ok(RequestStatsService.Build(statsRange, logs, accountNames, clientKeyNames));
        });
    }
}
