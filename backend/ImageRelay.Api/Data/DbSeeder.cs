namespace ImageRelay.Api.Data;

public class DbSeeder(
    AppDbContext db,
    IOptions<BootstrapOptions> bootstrap,
    IPasswordHasher hasher,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!await db.AdminUsers.AnyAsync(ct))
        {
            var cfg = bootstrap.Value;
            if (string.IsNullOrWhiteSpace(cfg.AdminUsername) || string.IsNullOrWhiteSpace(cfg.AdminPassword))
            {
                logger.LogWarning("Bootstrap admin credentials not configured; skipping admin seed.");
            }
            else
            {
                db.AdminUsers.Add(new AdminUser
                {
                    Username = cfg.AdminUsername,
                    PasswordHash = hasher.Hash(cfg.AdminPassword)
                });
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Seeded bootstrap admin user: {User}", cfg.AdminUsername);
            }
        }

        if (!await db.ModelMappings.AnyAsync(ct))
        {
            db.ModelMappings.Add(new ModelMapping
            {
                ExternalName = "gpt-5.4",
                UpstreamName = "gpt-5.4",
                IsEnabled = true
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded default model mapping: gpt-5.4 -> gpt-5.4");
        }
        else
        {
            var legacyMapping = await db.ModelMappings
                .FirstOrDefaultAsync(m => m.ExternalName == "gpt-5.4" && m.UpstreamName == "gpt-image-2", ct);
            if (legacyMapping is not null)
            {
                legacyMapping.UpstreamName = "gpt-5.4";
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Updated legacy model mapping: gpt-5.4 -> gpt-5.4");
            }
        }

        if (!await db.UpstreamHeaderSettings.AnyAsync(ct))
        {
            db.UpstreamHeaderSettings.Add(UpstreamHeaderSettings.CreateDefault());
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded default upstream header settings.");
        }
    }
}
