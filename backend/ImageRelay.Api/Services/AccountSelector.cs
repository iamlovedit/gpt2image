using System.Collections.Concurrent;

namespace ImageRelay.Api.Services;

public record AccountLease(UpstreamAccount Account, IDisposable ConcurrencyRelease);

public class AccountSelector(AppDbContext db, AccountConcurrencyRegistry registry, ILogger<AccountSelector> logger)
{
    public async Task<AccountLease?> PickAsync(HashSet<Guid> excluded, CancellationToken ct)
    {
        const int maxProbe = 16;
        var probed = 0;

        while (probed < maxProbe)
        {
            probed++;

            var q = db.UpstreamAccounts.AsQueryable()
                .Where(a => a.Status == UpstreamAccountStatus.Healthy)
                .Where(a => a.CoolingUntil == null || a.CoolingUntil < DateTime.UtcNow);

            if (excluded.Count > 0) q = q.Where(a => !excluded.Contains(a.Id));

            var account = await q
                .OrderBy(a => a.LastUsedAt ?? DateTime.MinValue)
                .FirstOrDefaultAsync(ct);

            if (account is null) return null;

            var release = registry.TryAcquire(account.Id, account.ConcurrencyLimit);
            if (release is null)
            {
                excluded.Add(account.Id);
                continue;
            }

            account.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new AccountLease(account, release);
        }

        logger.LogWarning("AccountSelector exceeded max probe attempts.");
        return null;
    }
}

public class AccountConcurrencyRegistry
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _map = new();

    public IDisposable? TryAcquire(Guid accountId, int limit)
    {
        var sem = _map.GetOrAdd(accountId, _ => new SemaphoreSlim(limit, limit));
        if (!sem.Wait(0)) return null;
        return new Releaser(sem);
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        private int _released;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) sem.Release();
        }
    }
}
