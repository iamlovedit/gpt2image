namespace ImageRelay.Api.Features.ClientKeys;

public record KeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    ClientApiKeyStatus Status,
    DateTime? ExpiresAt,
    int RpmLimit,
    int ConcurrencyLimit,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record KeyCreateRequest(
    string Name,
    DateTime? ExpiresAt,
    int? RpmLimit,
    int? ConcurrencyLimit,
    string? Notes);

public record KeyCreateResponse(KeyDto Key, string Plaintext);

public record KeyUpdateRequest(
    string? Name,
    ClientApiKeyStatus? Status,
    DateTime? ExpiresAt,
    int? RpmLimit,
    int? ConcurrencyLimit,
    string? Notes);

public static class KeysEndpoints
{
    public static void MapClientKeys(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/keys").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) =>
        {
            var rows = await db.ClientApiKeys.AsNoTracking()
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => ToDto(k))
                .ToListAsync();
            return Results.Ok(rows);
        });

        g.MapPost("/", async ([FromBody] KeyCreateRequest req, AppDbContext db, ApiKeyGenerator gen) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name required" });

            var generated = gen.Generate();
            var key = new ClientApiKey
            {
                Name = req.Name.Trim(),
                KeyPrefix = generated.Prefix,
                KeyHash = generated.Hash,
                ExpiresAt = req.ExpiresAt,
                RpmLimit = req.RpmLimit ?? 60,
                ConcurrencyLimit = req.ConcurrencyLimit ?? 4,
                Notes = req.Notes
            };
            db.ClientApiKeys.Add(key);
            await db.SaveChangesAsync();
            return Results.Ok(new KeyCreateResponse(ToDto(key), generated.Plaintext));
        });

        g.MapPatch("/{id:guid}", async (Guid id, [FromBody] KeyUpdateRequest req, AppDbContext db) =>
        {
            var k = await db.ClientApiKeys.FindAsync(id);
            if (k is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(req.Name)) k.Name = req.Name.Trim();
            if (req.Status.HasValue) k.Status = req.Status.Value;
            if (req.ExpiresAt.HasValue) k.ExpiresAt = req.ExpiresAt;
            if (req.RpmLimit is int rpm && rpm >= 0) k.RpmLimit = rpm;
            if (req.ConcurrencyLimit is int cc && cc >= 0) k.ConcurrencyLimit = cc;
            if (req.Notes is not null) k.Notes = req.Notes;
            await db.SaveChangesAsync();
            return Results.Ok(ToDto(k));
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var k = await db.ClientApiKeys.FindAsync(id);
            if (k is null) return Results.NotFound();
            db.ClientApiKeys.Remove(k);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static KeyDto ToDto(ClientApiKey k) => new(
        k.Id, k.Name, k.KeyPrefix, k.Status, k.ExpiresAt,
        k.RpmLimit, k.ConcurrencyLimit, k.Notes, k.CreatedAt, k.UpdatedAt);
}
