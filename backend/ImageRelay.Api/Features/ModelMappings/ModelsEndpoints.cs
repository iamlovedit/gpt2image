namespace ImageRelay.Api.Features.ModelMappings;

public record ModelMappingCreateRequest(
    string ExternalName,
    string UpstreamName,
    bool? IsEnabled);

public record ModelMappingUpdateRequest(
    string? ExternalName,
    string? UpstreamName,
    bool? IsEnabled);

public static class ModelsEndpoints
{
    private const int MaxModelNameLength = 128;

    public static void MapModelMappings(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/models").RequireAuthorization();

        g.MapGet("/", async (AppDbContext db) =>
        {
            var rows = await db.ModelMappings.AsNoTracking()
                .OrderBy(m => m.ExternalName)
                .ToListAsync();
            return Results.Ok(rows);
        });

        g.MapPost("/", async ([FromBody] ModelMappingCreateRequest req, AppDbContext db) =>
        {
            var validation = ValidateModelNames(req.ExternalName, req.UpstreamName);
            if (validation is not null) return validation;

            var externalName = req.ExternalName.Trim();
            var exists = await db.ModelMappings.AnyAsync(m => m.ExternalName == externalName);
            if (exists) return Results.Conflict(new { error = "externalName already exists" });

            var mapping = new ModelMapping
            {
                ExternalName = externalName,
                UpstreamName = req.UpstreamName.Trim(),
                IsEnabled = req.IsEnabled ?? true
            };
            db.ModelMappings.Add(mapping);
            await db.SaveChangesAsync();
            return Results.Ok(mapping);
        });

        g.MapPatch("/{id:guid}", async (Guid id, [FromBody] ModelMappingUpdateRequest req, AppDbContext db) =>
        {
            var mapping = await db.ModelMappings.FindAsync(id);
            if (mapping is null) return Results.NotFound();

            var externalName = req.ExternalName is null ? mapping.ExternalName : req.ExternalName.Trim();
            var upstreamName = req.UpstreamName is null ? mapping.UpstreamName : req.UpstreamName.Trim();
            var validation = ValidateModelNames(externalName, upstreamName);
            if (validation is not null) return validation;

            var exists = await db.ModelMappings
                .AnyAsync(m => m.Id != id && m.ExternalName == externalName);
            if (exists) return Results.Conflict(new { error = "externalName already exists" });

            mapping.ExternalName = externalName;
            mapping.UpstreamName = upstreamName;
            if (req.IsEnabled.HasValue) mapping.IsEnabled = req.IsEnabled.Value;
            mapping.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(mapping);
        });

        g.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var mapping = await db.ModelMappings.FindAsync(id);
            if (mapping is null) return Results.NotFound();

            db.ModelMappings.Remove(mapping);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static IResult? ValidateModelNames(string? externalName, string? upstreamName)
    {
        if (string.IsNullOrWhiteSpace(externalName))
            return Results.BadRequest(new { error = "externalName required" });
        if (string.IsNullOrWhiteSpace(upstreamName))
            return Results.BadRequest(new { error = "upstreamName required" });
        if (externalName.Trim().Length > MaxModelNameLength)
            return Results.BadRequest(new { error = "externalName too long" });
        if (upstreamName.Trim().Length > MaxModelNameLength)
            return Results.BadRequest(new { error = "upstreamName too long" });

        return null;
    }
}
