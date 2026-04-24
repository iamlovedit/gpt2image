using ImageRelay.Api.Features.Common;

namespace ImageRelay.Api.Features.Settings;

public record UpstreamHeaderSettingsDto(
    string UserAgent,
    string Version,
    string Originator,
    string? SessionId);

public record UpstreamHeaderSettingsUpdateRequest(
    string UserAgent,
    string Version,
    string Originator,
    string? SessionId);

public static class SettingsEndpoints
{
    public static void MapSettings(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/settings").RequireAuthorization();

        g.MapGet("/upstream-headers", async (AppDbContext db, CancellationToken ct) =>
        {
            var settings = await GetOrCreateAsync(db, ct);
            return ApiResponse.Ok(ToDto(settings));
        });

        g.MapPut("/upstream-headers", async (
            [FromBody] UpstreamHeaderSettingsUpdateRequest req,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userAgent = NormalizeRequired(req.UserAgent, "userAgent");
            if (userAgent is null) return ApiResponse.BadRequest("userAgent required");

            var version = NormalizeRequired(req.Version, "version");
            if (version is null) return ApiResponse.BadRequest("version required");

            var originator = NormalizeRequired(req.Originator, "originator");
            if (originator is null) return ApiResponse.BadRequest("originator required");

            var settings = await GetOrCreateAsync(db, ct);
            settings.UserAgent = userAgent;
            settings.Version = version;
            settings.Originator = originator;
            settings.SessionId = NormalizeOptional(req.SessionId);

            await db.SaveChangesAsync(ct);
            return ApiResponse.Ok(ToDto(settings));
        });
    }

    private static async Task<UpstreamHeaderSettings> GetOrCreateAsync(AppDbContext db, CancellationToken ct)
    {
        var settings = await db.UpstreamHeaderSettings.FirstOrDefaultAsync(x => x.Id == UpstreamHeaderSettings.SingletonId, ct);
        if (settings is not null) return settings;

        settings = UpstreamHeaderSettings.CreateDefault();
        db.UpstreamHeaderSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    private static UpstreamHeaderSettingsDto ToDto(UpstreamHeaderSettings settings) => new(
        ResolveRequired(settings.UserAgent, UpstreamHeaderSettings.DefaultUserAgent),
        ResolveRequired(settings.Version, UpstreamHeaderSettings.DefaultVersion),
        ResolveRequired(settings.Originator, UpstreamHeaderSettings.DefaultOriginator),
        NormalizeOptional(settings.SessionId));

    private static string ResolveRequired(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? NormalizeRequired(string? value, string _) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
