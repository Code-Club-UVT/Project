using GatewayAuth.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GatewayAuth.Authentication;

/// <summary>
/// Holds the public signing keys downloaded from the core API's JWKS and hands them to the token validator.
/// <list type="bullet">
/// <item>Keys are downloaded on first use and again every <see cref="GatewayAuthOptions.KeysRefreshMinutes"/>.</item>
/// <item>A token that names an unknown <c>kid</c> (the core API rotated its key) triggers an immediate download, so that same
/// request is accepted. After a successful download the next one waits at least
/// <see cref="GatewayAuthOptions.UnknownKeyRefreshMinutes"/>, so tokens with made-up key ids cannot make the API hammer the core API.</item>
/// <item>After a failed download (core API not up yet, network blip) the next try is allowed after a few seconds, not after the
/// full interval, so the API recovers shortly after the core API does. The previous keys stay in use meanwhile.</item>
/// </list>
/// The validator asks for keys synchronously, so a download blocks the request that triggers it for at most the HTTP timeout.
/// Other requests do not wait for it, except while no key has been loaded at all.
/// </summary>
internal sealed class JwksKeyStore(HttpClient httpClient, IOptions<GatewayAuthOptions> options, TimeProvider timeProvider, ILogger<JwksKeyStore> logger)
{
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ColdStartWait = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private volatile IReadOnlyList<SecurityKey> _keys = [];

    // UTC ticks, read with Volatile because DateTimeOffset is not read atomically.
    private long _lastSuccessTicks;
    private long _nextAttemptTicks;

    public IEnumerable<SecurityKey> GetKeys(string? kid)
    {
        var settings = options.Value;
        var matches = Match(_keys, kid);

        var now = timeProvider.GetUtcNow().UtcTicks;
        var stale = now - Volatile.Read(ref _lastSuccessTicks) >= TimeSpan.FromMinutes(settings.KeysRefreshMinutes).Ticks;
        if ((stale || matches.Count == 0) && now >= Volatile.Read(ref _nextAttemptTicks))
        {
            Refresh(settings);
            matches = Match(_keys, kid);
        }

        return matches;
    }

    private void Refresh(GatewayAuthOptions settings)
    {
        // Someone else is already downloading: if there are keys to work with, do not queue behind them. With no keys at
        // all (first requests after start) wait, because there is nothing to validate against.
        var wait = _keys.Count == 0 ? ColdStartWait : TimeSpan.Zero;
        if (!Monitor.TryEnter(_gate, wait))
        {
            return;
        }

        try
        {
            // Another request may have refreshed while this one waited.
            var now = timeProvider.GetUtcNow();
            if (now.UtcTicks < Volatile.Read(ref _nextAttemptTicks))
            {
                return;
            }

            try
            {
                var json = httpClient.GetStringAsync(settings.JwksUrl).GetAwaiter().GetResult();
                var keys = new JsonWebKeySet(json).GetSigningKeys().ToList();
                if (keys.Count == 0)
                {
                    logger.LogWarning("The JWKS at {Url} has no signing keys; keeping the previous {Count} key(s)", settings.JwksUrl, _keys.Count);
                    Volatile.Write(ref _nextAttemptTicks, now.Add(FailureBackoff).UtcTicks);
                    return;
                }

                _keys = keys;
                Volatile.Write(ref _lastSuccessTicks, now.UtcTicks);
                Volatile.Write(ref _nextAttemptTicks, now.AddMinutes(settings.UnknownKeyRefreshMinutes).UtcTicks);
                logger.LogInformation("Loaded {Count} signing key(s) from {Url}", keys.Count, settings.JwksUrl);
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _nextAttemptTicks, now.Add(FailureBackoff).UtcTicks);
                logger.LogWarning(ex, "Could not download the signing keys from {Url}; keeping the previous {Count} key(s), next try in {Seconds:N0}s", settings.JwksUrl, _keys.Count, FailureBackoff.TotalSeconds);
            }
        }
        finally
        {
            Monitor.Exit(_gate);
        }
    }

    private static List<SecurityKey> Match(IReadOnlyList<SecurityKey> keys, string? kid)
    {
        return kid is null ? keys.ToList() : keys.Where(k => k.KeyId == kid).ToList();
    }
}
