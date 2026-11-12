using Microsoft.Extensions.Caching.Memory;

namespace Gateway.Access;

/// <summary>
/// Small cache with two lifetimes: a value is "fresh" for <c>ttl</c>; after that it is reloaded, but if the reload fails
/// (database down) the old value is still used until it is older than <c>grace</c>. After that the failure is thrown.
/// Concurrent loads of the same key are collapsed into one.
/// </summary>
public sealed class TtlCache(IMemoryCache cache, TimeProvider timeProvider, ILogger<TtlCache> logger)
{
    private const int Stripes = 64;
    private readonly SemaphoreSlim[] _locks = Enumerable.Range(0, Stripes).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private sealed record Entry<T>(T Value, DateTimeOffset FetchedAt);

    public async Task<T> GetOrLoadAsync<T>(string key, TimeSpan ttl, TimeSpan grace, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
    {
        if (TryGetFresh(key, ttl, out T? fresh, out _))
        {
            return fresh!;
        }

        var gate = _locks[(uint)key.GetHashCode() % Stripes];
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Another request may have loaded it while this one waited.
            if (TryGetFresh(key, ttl, out fresh, out var stale))
            {
                return fresh!;
            }

            try
            {
                var value = await load(cancellationToken);
                cache.Set(key, new Entry<T>(value, timeProvider.GetUtcNow()), new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = grace });
                return value;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && stale is not null && timeProvider.GetUtcNow() - stale.FetchedAt < grace)
            {
                logger.LogWarning(ex, "Could not refresh {Key}; using the value from {Age:N0}s ago", key, (timeProvider.GetUtcNow() - stale.FetchedAt).TotalSeconds);
                return stale.Value;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetFresh<T>(string key, TimeSpan ttl, out T? value, out Entry<T>? entry)
    {
        if (cache.TryGetValue(key, out Entry<T>? found) && found is not null)
        {
            entry = found;
            if (timeProvider.GetUtcNow() - found.FetchedAt < ttl)
            {
                value = found.Value;
                return true;
            }
        }
        else
        {
            entry = null;
        }

        value = default;
        return false;
    }
}
