using ImageRelay.Api.Features.Common;

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
    string? Name,
    string? Email,
    string? Platform,
    string? AccountType,
    string? ProxyKey,
    int? Priority,
    decimal? RateMultiplier,
    bool? AutoPauseOnExpired,
    string? ChatGptUserId,
    string? ClientId,
    string? OrganizationId,
    string? PlanType,
    DateTime? SubscriptionExpiresAt,
    int? CodexPrimaryUsedPercent,
    int? CodexSecondaryUsedPercent,
    int? CodexPrimaryWindowMinutes,
    int? CodexSecondaryWindowMinutes,
    int? CodexPrimaryResetAfterSeconds,
    int? CodexSecondaryResetAfterSeconds,
    DateTime? CodexPrimaryResetAt,
    DateTime? CodexSecondaryResetAt,
    int? CodexPrimaryOverSecondaryLimitPercent,
    DateTime? CodexRateLimitUpdatedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public enum ImportDuplicateStrategy { Skip = 0, Overwrite = 1, Fail = 2 }

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

            return ApiResponse.Ok(new { total, page, pageSize, items = rows });
        });

        g.MapPost("/import", async ([FromBody] JsonElement req, AppDbContext db) =>
        {
            var payload = AccountImportParser.NormalizeImportItems(req);
            if (!payload.IsValidShape || payload.Items.Count == 0)
                return ApiResponse.BadRequest("items is empty");

            int inserted = 0, updated = 0, skipped = 0;
            foreach (var item in payload.Items)
            {
                if (string.IsNullOrWhiteSpace(item.AccessToken) || string.IsNullOrWhiteSpace(item.RefreshToken))
                { skipped++; continue; }

                var refreshToken = item.RefreshToken.Trim();
                var existing = await db.UpstreamAccounts
                    .FirstOrDefaultAsync(a => a.RefreshToken == refreshToken);

                if (existing is null)
                {
                    var account = new UpstreamAccount { Status = UpstreamAccountStatus.Healthy };
                    ApplyImport(account, item, overwriteMissing: true);
                    db.UpstreamAccounts.Add(account);
                    inserted++;
                }
                else
                {
                    switch (payload.Strategy)
                    {
                        case ImportDuplicateStrategy.Skip:
                            skipped++;
                            break;
                        case ImportDuplicateStrategy.Overwrite:
                            ApplyImport(existing, item, overwriteMissing: false);
                            existing.Status = UpstreamAccountStatus.Healthy;
                            existing.LastError = null;
                            updated++;
                            break;
                        case ImportDuplicateStrategy.Fail:
                            return ApiResponse.Conflict("duplicate refresh_token found");
                    }
                }
            }

            await db.SaveChangesAsync();
            return ApiResponse.Ok(new { inserted, updated, skipped });
        });

        g.MapPatch("/{id:guid}", async (Guid id, [FromBody] AccountUpdateRequest req, AppDbContext db) =>
        {
            var a = await db.UpstreamAccounts.FindAsync(id);
            if (a is null) return ApiResponse.NotFound();
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
            return ApiResponse.Ok(ToDto(a));
        });

        g.MapPost("/{id:guid}/refresh", async (Guid id, AppDbContext db, TokenRefresher refresher, CancellationToken ct) =>
        {
            var a = await db.UpstreamAccounts.FindAsync([id], ct);
            if (a is null) return ApiResponse.NotFound();
            try
            {
                await refresher.EnsureFreshAsync(a, force: true, ct);
                return ApiResponse.Ok(ToDto(a));
            }
            catch (Exception ex)
            {
                return ApiResponse.BadRequest(ex.Message);
            }
        });

        g.MapPost("/{id:guid}/test", async (Guid id, AppDbContext db, AccountConnectivityTester tester, CancellationToken ct) =>
        {
            var a = await db.UpstreamAccounts.FindAsync([id], ct);
            if (a is null) return ApiResponse.NotFound();

            var result = await tester.TestAsync(db, a, ct);
            return ApiResponse.Ok(result);
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var a = await db.UpstreamAccounts.FindAsync(id);
            if (a is null) return ApiResponse.NotFound();
            db.UpstreamAccounts.Remove(a);
            await db.SaveChangesAsync();
            return ApiResponse.Ok();
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
        a.Name,
        a.Email,
        a.Platform,
        a.AccountType,
        a.ProxyKey,
        a.Priority,
        a.RateMultiplier,
        a.AutoPauseOnExpired,
        a.ChatGptUserId,
        a.ClientId,
        a.OrganizationId,
        a.PlanType,
        a.SubscriptionExpiresAt,
        a.CodexPrimaryUsedPercent,
        a.CodexSecondaryUsedPercent,
        a.CodexPrimaryWindowMinutes,
        a.CodexSecondaryWindowMinutes,
        a.CodexPrimaryResetAfterSeconds,
        a.CodexSecondaryResetAfterSeconds,
        a.CodexPrimaryResetAt,
        a.CodexSecondaryResetAt,
        a.CodexPrimaryOverSecondaryLimitPercent,
        a.CodexRateLimitUpdatedAt,
        a.CreatedAt,
        a.UpdatedAt);

    private static void ApplyImport(UpstreamAccount account, AccountImportItem item, bool overwriteMissing)
    {
        account.AccessToken = item.AccessToken!.Trim();
        account.RefreshToken = item.RefreshToken!.Trim();
        ApplyString(item.ChatGptAccountId, v => account.ChatGptAccountId = v, overwriteMissing);
        ApplyString(item.Notes, v => account.Notes = v, overwriteMissing);
        ApplyString(item.Name, v => account.Name = v, overwriteMissing);
        ApplyString(item.Email, v => account.Email = v, overwriteMissing);
        ApplyString(item.Platform, v => account.Platform = v, overwriteMissing);
        ApplyString(item.AccountType, v => account.AccountType = v, overwriteMissing);
        ApplyString(item.ProxyKey, v => account.ProxyKey = v, overwriteMissing);
        ApplyString(item.ChatGptUserId, v => account.ChatGptUserId = v, overwriteMissing);
        ApplyString(item.ClientId, v => account.ClientId = v, overwriteMissing);
        ApplyString(item.OrganizationId, v => account.OrganizationId = v, overwriteMissing);
        ApplyString(item.PlanType, v => account.PlanType = v, overwriteMissing);
        ApplyString(item.RawMetadataJson, v => account.RawMetadataJson = v, overwriteMissing);

        if (item.AccessTokenExpiresAt.HasValue || overwriteMissing) account.AccessTokenExpiresAt = item.AccessTokenExpiresAt;
        if (item.SubscriptionExpiresAt.HasValue || overwriteMissing) account.SubscriptionExpiresAt = item.SubscriptionExpiresAt;
        if (item.Priority.HasValue || overwriteMissing) account.Priority = item.Priority;
        if (item.RateMultiplier.HasValue || overwriteMissing) account.RateMultiplier = item.RateMultiplier;
        if (item.AutoPauseOnExpired.HasValue || overwriteMissing) account.AutoPauseOnExpired = item.AutoPauseOnExpired;
        if (item.ConcurrencyLimit is int concurrency && concurrency > 0) account.ConcurrencyLimit = concurrency;
    }

    private static void ApplyString(string? value, Action<string?> set, bool overwriteMissing)
    {
        if (value is not null || overwriteMissing) set(NormalizeOptional(value));
    }

    private static string Preview(string token) =>
        string.IsNullOrEmpty(token) ? "" :
        token.Length <= 12 ? token : $"{token[..6]}…{token[^4..]}";

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
