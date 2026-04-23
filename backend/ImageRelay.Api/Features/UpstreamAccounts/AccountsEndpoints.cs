using ImageRelay.Api.Data;
using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ImageRelay.Api.Features.UpstreamAccounts;

public record AccountDto(
    Guid Id,
    string AccessTokenPreview,
    string RefreshTokenPreview,
    string? ChatGptAccountId,
    DateTime? AccessTokenExpiresAt,
    UpstreamAccountStatus Status,
    DateTime? CoolingUntil,
    string? LastError,
    DateTime? LastUsedAt,
    long SuccessCount,
    long FailureCount,
    int ConcurrencyLimit,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public enum ImportDuplicateStrategy { Skip = 0, Overwrite = 1, Fail = 2 }

public record ImportRequest(List<JsonElement> Items, ImportDuplicateStrategy Strategy);

public record AccountUpdateRequest(
    UpstreamAccountStatus? Status,
    string? Notes,
    int? ConcurrencyLimit,
    string? ChatGptAccountId);

public static class AccountsEndpoints
{
    public static void MapUpstreamAccounts(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/accounts").RequireAuthorization();

        g.MapGet("/", async (
            AppDbContext db,
            [FromQuery] UpstreamAccountStatus? status,
            [FromQuery] string? keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = db.UpstreamAccounts.AsNoTracking().AsQueryable();
            if (status.HasValue) q = q.Where(a => a.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var k = keyword.Trim();
                q = q.Where(a => (a.Notes ?? "").Contains(k) || (a.LastError ?? "").Contains(k));
            }

            var total = await q.CountAsync();
            var rows = await q
                .OrderByDescending(a => a.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => ToDto(a))
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items = rows });
        });

        g.MapPost("/import", async ([FromBody] ImportRequest req, AppDbContext db) =>
        {
            if (req.Items is null || req.Items.Count == 0)
                return Results.BadRequest(new { error = "items is empty" });

            int inserted = 0, updated = 0, skipped = 0;
            foreach (var item in req.Items)
            {
                var accessToken = ReadString(item, "accessToken", "access_token");
                var refreshToken = ReadString(item, "refreshToken", "refresh_token");
                var accountId = ReadOptionalString(item, "chatgptAccountId", "chatgpt_account_id");

                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
                { skipped++; continue; }

                var existing = await db.UpstreamAccounts
                    .FirstOrDefaultAsync(a => a.RefreshToken == refreshToken);

                if (existing is null)
                {
                    db.UpstreamAccounts.Add(new UpstreamAccount
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ChatGptAccountId = accountId.Value,
                        Status = UpstreamAccountStatus.Healthy
                    });
                    inserted++;
                }
                else
                {
                    switch (req.Strategy)
                    {
                        case ImportDuplicateStrategy.Skip:
                            skipped++;
                            break;
                        case ImportDuplicateStrategy.Overwrite:
                            existing.AccessToken = accessToken;
                            existing.RefreshToken = refreshToken;
                            if (accountId.HasValue) existing.ChatGptAccountId = accountId.Value;
                            existing.Status = UpstreamAccountStatus.Healthy;
                            existing.LastError = null;
                            existing.AccessTokenExpiresAt = null;
                            updated++;
                            break;
                        case ImportDuplicateStrategy.Fail:
                            return Results.Conflict(new { error = "duplicate refresh_token found" });
                    }
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { inserted, updated, skipped });
        });

        g.MapPatch("/{id:guid}", async (Guid id, [FromBody] AccountUpdateRequest req, AppDbContext db) =>
        {
            var a = await db.UpstreamAccounts.FindAsync(id);
            if (a is null) return Results.NotFound();
            if (req.Status.HasValue)
            {
                a.Status = req.Status.Value;
                if (req.Status.Value == UpstreamAccountStatus.Healthy)
                {
                    a.CoolingUntil = null;
                    a.LastError = null;
                }
            }
            if (req.Notes is not null) a.Notes = req.Notes;
            if (req.ConcurrencyLimit is int c && c > 0) a.ConcurrencyLimit = c;
            if (req.ChatGptAccountId is not null) a.ChatGptAccountId = NormalizeOptional(req.ChatGptAccountId);
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(a));
        });

        g.MapPost("/{id:guid}/refresh", async (Guid id, AppDbContext db, TokenRefresher refresher, CancellationToken ct) =>
        {
            var a = await db.UpstreamAccounts.FindAsync([id], ct);
            if (a is null) return Results.NotFound();
            try
            {
                await refresher.EnsureFreshAsync(a, force: true, ct);
                return Results.Ok(ToDto(a));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var a = await db.UpstreamAccounts.FindAsync(id);
            if (a is null) return Results.NotFound();
            db.UpstreamAccounts.Remove(a);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static AccountDto ToDto(UpstreamAccount a) => new(
        a.Id,
        Preview(a.AccessToken),
        Preview(a.RefreshToken),
        a.ChatGptAccountId,
        a.AccessTokenExpiresAt,
        a.Status,
        a.CoolingUntil,
        a.LastError,
        a.LastUsedAt,
        a.SuccessCount,
        a.FailureCount,
        a.ConcurrencyLimit,
        a.Notes,
        a.CreatedAt,
        a.UpdatedAt);

    private static string Preview(string token) =>
        string.IsNullOrEmpty(token) ? "" :
        token.Length <= 12 ? token : $"{token[..6]}…{token[^4..]}";

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static (bool HasValue, string? Value) ReadOptionalString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var prop)) continue;
            return prop.ValueKind switch
            {
                JsonValueKind.String => (true, NormalizeOptional(prop.GetString())),
                JsonValueKind.Null => (true, null),
                _ => (true, null)
            };
        }

        return (false, null);
    }

    private static string? ReadString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(item, name, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.String) return prop.GetString();
            if (prop.ValueKind == JsonValueKind.Null) return null;
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement item, string name, out JsonElement value)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in item.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
