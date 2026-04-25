using System.Collections.Concurrent;

namespace ImageRelay.Api.Services;

public enum RateLimitReason { None, Rpm, Concurrency }

public record RateLimitLease(IDisposable Release, bool Ok, RateLimitReason Reason);

public class ClientRateLimiter : IClientRateLimiter
{
    private readonly ConcurrentDictionary<Guid, KeyBucket> _buckets = new();

    public RateLimitLease TryAcquire(ClientApiKey key)
    {
        var bucket = _buckets.GetOrAdd(key.Id, _ => new KeyBucket());
        return bucket.TryAcquire(key.RpmLimit, key.ConcurrencyLimit);
    }

    private sealed class KeyBucket
    {
        private readonly object _gate = new();
        private readonly Queue<DateTime> _window = new();
        private int _inflight;

        public RateLimitLease TryAcquire(int rpmLimit, int concurrencyLimit)
        {
            lock (_gate)
            {
                var now = DateTime.UtcNow;
                var cutoff = now.AddMinutes(-1);
                while (_window.Count > 0 && _window.Peek() < cutoff) _window.Dequeue();

                if (rpmLimit > 0 && _window.Count >= rpmLimit)
                    return new RateLimitLease(NoopReleaser.Instance, false, RateLimitReason.Rpm);

                if (concurrencyLimit > 0 && _inflight >= concurrencyLimit)
                    return new RateLimitLease(NoopReleaser.Instance, false, RateLimitReason.Concurrency);

                _window.Enqueue(now);
                _inflight++;
                return new RateLimitLease(new Releaser(this), true, RateLimitReason.None);
            }
        }

        public void Release()
        {
            lock (_gate) { _inflight = Math.Max(0, _inflight - 1); }
        }

        private sealed class Releaser(KeyBucket owner) : IDisposable
        {
            private int _released;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0) owner.Release();
            }
        }

        private sealed class NoopReleaser : IDisposable
        {
            public static readonly NoopReleaser Instance = new();
            public void Dispose() { }
        }
    }
}
