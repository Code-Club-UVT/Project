using System.Collections.Concurrent;
using Identity.Options;
using Microsoft.Extensions.Options;

namespace Identity.Services.Auth;

public sealed class AttemptLimiter(IOptions<ThrottleOptions> options, TimeProvider timeProvider) : IAttemptLimiter
{
    // Bounds the memory: names made up by an attacker cannot grow the table without limit. The gateway's per-IP limits
    // still apply when tracking stops.
    private const int SweepAbove = 10_000;
    private const int MaxEntries = 50_000;

    private static readonly TimeSpan EmailWindow = TimeSpan.FromHours(1);

    private readonly ThrottleOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public bool IsLoginBlocked(string identifier)
    {
        if (!_entries.TryGetValue(LoginKey(identifier), out var entry))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        lock (entry)
        {
            Trim(entry, now - LoginWindow);
            return entry.Times.Count >= _options.LoginFailures;
        }
    }

    public void RecordLoginFailure(string identifier)
    {
        var entry = GetOrAdd(LoginKey(identifier));
        if (entry is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        lock (entry)
        {
            Trim(entry, now - LoginWindow);
            entry.Times.Enqueue(now);
        }
    }

    public void ClearLogin(string identifier)
    {
        _entries.TryRemove(LoginKey(identifier), out _);
    }

    public bool TryAcquireEmail(string address)
    {
        var entry = GetOrAdd(EmailKey(address));
        if (entry is null)
        {
            return true;
        }

        var now = timeProvider.GetUtcNow();
        lock (entry)
        {
            Trim(entry, now - EmailWindow);

            var tooSoon = entry.Times.Count > 0 && now - entry.Newest < TimeSpan.FromSeconds(_options.EmailMinIntervalSeconds);
            if (tooSoon || entry.Times.Count >= _options.EmailsPerHour)
            {
                return false;
            }

            entry.Times.Enqueue(now);
            entry.Newest = now;
            return true;
        }
    }

    private TimeSpan LoginWindow => TimeSpan.FromMinutes(_options.LoginWindowMinutes);

    private static string LoginKey(string identifier) => "login:" + identifier.Trim().ToLowerInvariant();

    private static string EmailKey(string address) => "email:" + address.Trim().ToLowerInvariant();

    private Entry? GetOrAdd(string key)
    {
        if (_entries.Count > SweepAbove)
        {
            Sweep();
        }

        if (_entries.Count >= MaxEntries && !_entries.ContainsKey(key))
        {
            return null;
        }

        return _entries.GetOrAdd(key, _ => new Entry());
    }

    /// <summary>Drops the entries whose times are all older than the longest window.</summary>
    private void Sweep()
    {
        var cutoff = timeProvider.GetUtcNow() - (LoginWindow > EmailWindow ? LoginWindow : EmailWindow);
        foreach (var (key, entry) in _entries)
        {
            bool expired;
            lock (entry)
            {
                Trim(entry, cutoff);
                expired = entry.Times.Count == 0;
            }

            if (expired)
            {
                _entries.TryRemove(key, out _);
            }
        }
    }

    private static void Trim(Entry entry, DateTimeOffset cutoff)
    {
        while (entry.Times.Count > 0 && entry.Times.Peek() <= cutoff)
        {
            entry.Times.Dequeue();
        }
    }

    private sealed class Entry
    {
        public Queue<DateTimeOffset> Times { get; } = new();

        /// <summary>The time of the last email, meaningful while <see cref="Times"/> is not empty.</summary>
        public DateTimeOffset Newest { get; set; }
    }
}
