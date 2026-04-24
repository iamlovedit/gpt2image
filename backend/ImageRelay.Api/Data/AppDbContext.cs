using ImageRelay.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImageRelay.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<UpstreamAccount> UpstreamAccounts => Set<UpstreamAccount>();
    public DbSet<ClientApiKey> ClientApiKeys => Set<ClientApiKey>();
    public DbSet<ModelMapping> ModelMappings => Set<ModelMapping>();
    public DbSet<RequestLog> RequestLogs => Set<RequestLog>();
    public DbSet<UpstreamHeaderSettings> UpstreamHeaderSettings => Set<UpstreamHeaderSettings>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AdminUser>(e =>
        {
            e.ToTable("admin_users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        });

        b.Entity<UpstreamAccount>(e =>
        {
            e.ToTable("upstream_accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.AccessToken).IsRequired();
            e.Property(x => x.RefreshToken).IsRequired();
            e.Property(x => x.ChatGptAccountId).HasMaxLength(256);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Email).HasMaxLength(320);
            e.Property(x => x.Platform).HasMaxLength(64);
            e.Property(x => x.AccountType).HasMaxLength(64);
            e.Property(x => x.ProxyKey).HasMaxLength(512);
            e.Property(x => x.RateMultiplier).HasPrecision(18, 6);
            e.Property(x => x.ChatGptUserId).HasMaxLength(256);
            e.Property(x => x.ClientId).HasMaxLength(256);
            e.Property(x => x.OrganizationId).HasMaxLength(256);
            e.Property(x => x.PlanType).HasMaxLength(128);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.Status, x.LastUsedAt });
        });

        b.Entity<ClientApiKey>(e =>
        {
            e.ToTable("client_api_keys");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(128).IsRequired();
            e.Property(x => x.KeyPrefix).HasMaxLength(16).IsRequired();
            e.Property(x => x.KeyHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => x.KeyHash).IsUnique();
        });

        b.Entity<ModelMapping>(e =>
        {
            e.ToTable("model_mappings");
            e.HasKey(x => x.Id);
            e.Property(x => x.ExternalName).HasMaxLength(128).IsRequired();
            e.Property(x => x.UpstreamName).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.ExternalName).IsUnique();
        });

        b.Entity<RequestLog>(e =>
        {
            e.ToTable("request_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.RequestId).HasMaxLength(64).IsRequired();
            e.Property(x => x.ExternalModel).HasMaxLength(128);
            e.Property(x => x.UpstreamModel).HasMaxLength(128);
            e.Property(x => x.BusinessStatus).HasConversion<int>();
            e.Property(x => x.ErrorType).HasMaxLength(128);
            e.Property(x => x.ErrorMessage).HasMaxLength(2048);
            e.HasIndex(x => x.StartedAt);
            e.HasIndex(x => x.ClientKeyId);
            e.HasIndex(x => x.UpstreamAccountId);
        });

        b.Entity<UpstreamHeaderSettings>(e =>
        {
            e.ToTable("upstream_header_settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserAgent).HasMaxLength(512).IsRequired();
            e.Property(x => x.Version).HasMaxLength(128).IsRequired();
            e.Property(x => x.Originator).HasMaxLength(128).IsRequired();
            e.Property(x => x.SessionId).HasMaxLength(256);
        });
    }

    public override int SaveChanges()
    {
        TouchTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void TouchTimestamps()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified) continue;
            var prop = entry.Metadata.FindProperty("UpdatedAt");
            if (prop is not null) entry.Property("UpdatedAt").CurrentValue = now;
        }
    }
}
