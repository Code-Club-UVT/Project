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
    private static readonly TimeSpan SweepEvery = TimeSpan.FromMinutes(1);

    private long _nextSweepTicks;

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
        var entry = GetOrAdd(LoginKey(identifier), LoginWindow);
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
        var entry = GetOrAdd(EmailKey(address), EmailWindow);
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

    private Entry? GetOrAdd(string key, TimeSpan window)
    {
        // Above the threshold, at most one sweep a minute: sweeping on every call would make each request walk the whole table.
        if (_entries.Count > SweepAbove)
        {
            var nowTicks = timeProvider.GetUtcNow().UtcTicks;
            var due = Volatile.Read(ref _nextSweepTicks);
            if (nowTicks >= due && Interlocked.CompareExchange(ref _nextSweepTicks, nowTicks + SweepEvery.Ticks, due) == due)
            {
                Sweep();
            }
        }

        if (_entries.Count >= MaxEntries && !_entries.ContainsKey(key))
        {
            return null;
        }

        return _entries.GetOrAdd(key, _ => new Entry(window));
    }

    /// <summary>Drops the entries whose times are all older than their own window.</summary>
    private void Sweep()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var (key, entry) in _entries)
        {
            bool expired;
            lock (entry)
            {
                Trim(entry, now - entry.Window);
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

    private sealed class Entry(TimeSpan window)
    {
        /// <summary>How long a time counts: failed logins and emails have different windows.</summary>
        public TimeSpan Window { get; } = window;

        public Queue<DateTimeOffset> Times { get; } = new();

        /// <summary>The time of the last email, meaningful while <see cref="Times"/> is not empty.</summary>
        public DateTimeOffset Newest { get; set; }
    }
}
