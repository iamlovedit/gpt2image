namespace ImageRelay.Api.Middleware;

public static class ClientApiKeyAuth
{
    public const string ContextKey = "ClientApiKey";

    public static async Task<ClientApiKey?> ResolveAsync(HttpContext ctx, AppDbContext db, ApiKeyGenerator keyGen)
    {
        var auth = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = auth["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token)) return null;

        var hash = keyGen.HashKey(token);
        var key = await db.ClientApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash);
        if (key is null) return null;
        if (key.Status != ClientApiKeyStatus.Active) return null;
        if (key.ExpiresAt is DateTime exp && exp < DateTime.UtcNow) return null;

        ctx.Items[ContextKey] = key;
        return key;
    }
}
