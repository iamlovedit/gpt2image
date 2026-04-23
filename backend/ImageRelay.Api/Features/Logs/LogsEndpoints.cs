using ImageRelay.Api.Data;
using ImageRelay.Api.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageRelay.Api.Features.Logs;

public static class LogsEndpoints
{
    public static void MapLogs(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/logs").RequireAuthorization();

        g.MapGet("/", async (
            AppDbContext db,
            [FromQuery] RequestBusinessStatus? status,
            [FromQuery] Guid? clientKeyId,
            [FromQuery] Guid? accountId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = db.RequestLogs.AsNoTracking().AsQueryable();
            if (status.HasValue) q = q.Where(l => l.BusinessStatus == status.Value);
            if (clientKeyId.HasValue) q = q.Where(l => l.ClientKeyId == clientKeyId.Value);
            if (accountId.HasValue) q = q.Where(l => l.UpstreamAccountId == accountId.Value);
            if (from.HasValue) q = q.Where(l => l.StartedAt >= from.Value);
            if (to.HasValue) q = q.Where(l => l.StartedAt <= to.Value);

            var total = await q.CountAsync();
            var rows = await q
                .OrderByDescending(l => l.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return Results.Ok(new { total, page, pageSize, items = rows });
        });

        g.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var log = await db.RequestLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
            return log is null ? Results.NotFound() : Results.Ok(log);
        });
    }
}
