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

    private async Task PatchSchemaAsync(CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE upstream_accounts
            ADD COLUMN IF NOT EXISTS "ChatGptAccountId" character varying(256);

            ALTER TABLE upstream_accounts
            ADD COLUMN IF NOT EXISTS "Name" character varying(256),
            ADD COLUMN IF NOT EXISTS "Email" character varying(320),
            ADD COLUMN IF NOT EXISTS "Platform" character varying(64),
            ADD COLUMN IF NOT EXISTS "AccountType" character varying(64),
            ADD COLUMN IF NOT EXISTS "ProxyKey" character varying(512),
            ADD COLUMN IF NOT EXISTS "Priority" integer,
            ADD COLUMN IF NOT EXISTS "RateMultiplier" numeric(18,6),
            ADD COLUMN IF NOT EXISTS "AutoPauseOnExpired" boolean,
            ADD COLUMN IF NOT EXISTS "ChatGptUserId" character varying(256),
            ADD COLUMN IF NOT EXISTS "ClientId" character varying(256),
            ADD COLUMN IF NOT EXISTS "OrganizationId" character varying(256),
            ADD COLUMN IF NOT EXISTS "PlanType" character varying(128),
            ADD COLUMN IF NOT EXISTS "SubscriptionExpiresAt" timestamp with time zone,
            ADD COLUMN IF NOT EXISTS "RawMetadataJson" text;
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

        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE request_logs
            ADD COLUMN IF NOT EXISTS "InputTokens" bigint,
            ADD COLUMN IF NOT EXISTS "OutputTokens" bigint,
            ADD COLUMN IF NOT EXISTS "TotalTokens" bigint,
            ADD COLUMN IF NOT EXISTS "ImageInputTokens" bigint,
            ADD COLUMN IF NOT EXISTS "ImageOutputTokens" bigint,
            ADD COLUMN IF NOT EXISTS "ImageTotalTokens" bigint;
            """,
            ct);
    }
}
