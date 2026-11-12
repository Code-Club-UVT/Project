using Microsoft.Extensions.Caching.Memory;

namespace Gateway.Tokens;

public interface IProjectTokenProvider
{
    /// <summary>A valid project token for the user. Cached until shortly before it expires.</summary>
    Task<TokenResult> GetAsync(Guid userId, string slug, Guid roleId, CancellationToken cancellationToken);
}

/// <summary>
/// Caches the tokens identity issues, so identity is called about once per token lifetime per user and project.
/// The role id is part of the key, so a role change gets a new token without waiting for the old one to expire.
/// </summary>
public sealed class ProjectTokenProvider(IdentityTokenClient client, IMemoryCache cache, TimeProvider timeProvider) : IProjectTokenProvider
{
    private const int Stripes = 64;
    private static readonly TimeSpan MaxMargin = TimeSpan.FromSeconds(60);

    // Shared by all instances (the provider is scoped, the locks must not be).
    private static readonly SemaphoreSlim[] Locks = Enumerable.Range(0, Stripes).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<TokenResult> GetAsync(Guid userId, string slug, Guid roleId, CancellationToken cancellationToken)
    {
        var key = $"token:{userId:N}:{slug}:{roleId:N}";
        if (cache.TryGetValue(key, out TokenResult? cached) && cached is not null)
        {
            return cached;
        }

        var gate = Locks[(uint)key.GetHashCode() % Stripes];
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null)
            {
                return cached;
            }

            var result = await client.IssueAsync(userId, slug, cancellationToken);
            if (result.Outcome == TokenOutcome.Issued)
            {
                // Stop using it a bit before it expires (a quarter of its life, at most 60 seconds).
                var now = timeProvider.GetUtcNow();
                var lifetime = result.ExpiresAt - now;
                var margin = TimeSpan.FromTicks(Math.Min(lifetime.Ticks / 4, MaxMargin.Ticks));
                var cacheFor = lifetime - margin;
                if (cacheFor > TimeSpan.Zero)
                {
                    cache.Set(key, result, cacheFor);
                }
            }

            return result;
        }
        finally
        {
            gate.Release();
        }
    }
}
