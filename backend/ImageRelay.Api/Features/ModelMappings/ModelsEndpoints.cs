using ImageRelay.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ImageRelay.Api.Features.ModelMappings;

public static class ModelsEndpoints
{
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
    }
}
