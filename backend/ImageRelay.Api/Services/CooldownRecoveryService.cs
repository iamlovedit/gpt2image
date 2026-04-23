using ImageRelay.Api.Data;
using ImageRelay.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImageRelay.Api.Services;

public class CooldownRecoveryService(IServiceScopeFactory scopeFactory, ILogger<CooldownRecoveryService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var ready = await db.UpstreamAccounts
                    .Where(a => a.Status == UpstreamAccountStatus.Cooling && a.CoolingUntil != null && a.CoolingUntil < now)
                    .ToListAsync(stoppingToken);
                foreach (var a in ready)
                {
                    a.Status = UpstreamAccountStatus.Healthy;
                    a.CoolingUntil = null;
                }
                if (ready.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Recovered {Count} accounts from cooling.", ready.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cooldown recovery iteration failed.");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
