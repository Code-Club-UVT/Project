using System.Collections.Concurrent;
using Identity.Options;
using Microsoft.Extensions.Options;

namespace Identity.Services.Auth;

public sealed class AttemptLimiter(IOptions<ThrottleOptions> options, TimeProvider timeProvider, ILogger<AttemptLimiter> logger) : IAttemptLimiter
{
    // Bounds the memory: names made up by an attacker cannot grow the table without limit. The gateway's per-IP limits
    // still apply when tracking stops.
    private const int SweepAbove = 10_000;
    private const int MaxEntries = 50_000;

    private static readonly TimeSpan EmailWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan SweepEvery = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan GlobalDay = TimeSpan.FromHours(24);
    private static readonly TimeSpan GlobalLogEvery = TimeSpan.FromMinutes(1);

    private long _nextSweepTicks;

    // Every email that was allowed, to any address: one queue per window, both guarded by _globalGate.
    private readonly object _globalGate = new();
    private readonly Queue<DateTimeOffset> _globalHour = new();
    private readonly Queue<DateTimeOffset> _globalDay = new();
    private DateTimeOffset _nextGlobalLog = DateTimeOffset.MinValue;

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

    public EmailPermit TryAcquireEmail(string address)
    {
        var entry = GetOrAdd(EmailKey(address), EmailWindow);
        var now = timeProvider.GetUtcNow();
        if (entry is null)
        {
            // The table is full, so this address is not tracked; the total still is.
            return TryAcquireGlobal(now) ? EmailPermit.Allowed : EmailPermit.GlobalLimited;
        }

        lock (entry)
        {
            Trim(entry, now - EmailWindow);

            var tooSoon = entry.Times.Count > 0 && now - entry.Newest < TimeSpan.FromSeconds(_options.EmailMinIntervalSeconds);
            if (tooSoon || entry.Times.Count >= _options.EmailsPerHour)
            {
                return EmailPermit.AddressLimited;
            }

            // Asked last, so an email refused for its address does not use up the total, and one refused for the total
            // is not counted against its address.
            if (!TryAcquireGlobal(now))
            {
                return EmailPermit.GlobalLimited;
            }

            entry.Times.Enqueue(now);
            entry.Newest = now;
            return EmailPermit.Allowed;
        }
    }

    private bool TryAcquireGlobal(DateTimeOffset now)
    {
        lock (_globalGate)
        {
            Trim(_globalHour, now - EmailWindow);
            Trim(_globalDay, now - GlobalDay);

            if (_globalHour.Count >= _options.GlobalEmailsPerHour || _globalDay.Count >= _options.GlobalEmailsPerDay)
            {
                // Someone should look at this (an attack, or a limit that is too low), but not once per refused request.
                if (now >= _nextGlobalLog)
                {
                    _nextGlobalLog = now + GlobalLogEvery;
                    logger.LogError(
                        "The global email limit is reached ({Hour} of {HourLimit} in the last hour, {Day} of {DayLimit} in the last 24 hours): no more emails are sent until it frees up",
                        _globalHour.Count, _options.GlobalEmailsPerHour, _globalDay.Count, _options.GlobalEmailsPerDay);
                }

                return false;
            }

            _globalHour.Enqueue(now);
            _globalDay.Enqueue(now);
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

    private static void Trim(Entry entry, DateTimeOffset cutoff) => Trim(entry.Times, cutoff);

    private static void Trim(Queue<DateTimeOffset> times, DateTimeOffset cutoff)
    {
        while (times.Count > 0 && times.Peek() <= cutoff)
        {
            times.Dequeue();
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
