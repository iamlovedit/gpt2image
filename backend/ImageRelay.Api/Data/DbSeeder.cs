using ImageRelay.Api.Configuration;
using ImageRelay.Api.Data.Entities;
using ImageRelay.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ImageRelay.Api.Data;

public class DbSeeder(
    AppDbContext db,
    IOptions<BootstrapOptions> bootstrap,
    PasswordHasher hasher,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        await PatchSchemaAsync(ct);

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
                UpstreamName = "gpt-image-2",
                IsEnabled = true
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded default model mapping: gpt-5.4 -> gpt-image-2");
        }

        if (!await db.UpstreamHeaderSettings.AnyAsync(ct))
        {
            db.UpstreamHeaderSettings.Add(UpstreamHeaderSettings.CreateDefault());
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded default upstream header settings.");
        }
    }

    private async Task PatchSchemaAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE upstream_accounts
            ADD COLUMN IF NOT EXISTS "ChatGptAccountId" character varying(256);
            """,
            ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS upstream_header_settings (
                "Id" integer PRIMARY KEY,
                "UserAgent" character varying(512) NOT NULL,
                "Version" character varying(128) NOT NULL,
                "Originator" character varying(128) NOT NULL,
                "SessionId" character varying(256),
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL
            );
            """,
            ct);
    }
}
