using Gateway.Access;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gateway.Tests;

public sealed class TtlCacheTests : IDisposable
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(300);

    private readonly FakeTimeProvider _clock = new();
    private readonly MemoryCache _memory = new(new MemoryCacheOptions());
    private readonly TtlCache _cache;

    public TtlCacheTests()
    {
        _cache = new TtlCache(_memory, _clock, NullLogger<TtlCache>.Instance);
    }

    public void Dispose() => _memory.Dispose();

    private Task<int> Get(string key, Func<CancellationToken, Task<int>> load, CancellationToken token = default)
        => _cache.GetOrLoadAsync(key, Ttl, Grace, load, token);

    [Fact]
    public async Task The_first_use_loads_and_a_fresh_value_is_not_loaded_again()
    {
        var loads = 0;
        Task<int> Load(CancellationToken _) => Task.FromResult(++loads);

        var first = await Get("k", Load);
        _clock.Advance(Ttl - TimeSpan.FromSeconds(1));
        var second = await Get("k", Load);

        Assert.Equal((1, 1), (first, second));
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task A_value_older_than_the_ttl_is_loaded_again()
    {
        var loads = 0;
        Task<int> Load(CancellationToken _) => Task.FromResult(++loads);
        await Get("k", Load);

        _clock.Advance(Ttl);
        var again = await Get("k", Load);

        Assert.Equal(2, again);
    }

    [Fact]
    public async Task When_the_reload_fails_the_old_value_is_used_until_the_grace_ends()
    {
        await Get("k", _ => Task.FromResult(42));
        _clock.Advance(TimeSpan.FromSeconds(120));

        var stale = await Get("k", _ => throw new InvalidOperationException("database down"));

        Assert.Equal(42, stale);
    }

    [Fact]
    public async Task After_the_grace_the_failure_is_thrown_so_access_is_never_granted_on_old_data()
    {
        await Get("k", _ => Task.FromResult(42));
        _clock.Advance(Grace);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Get("k", _ => throw new InvalidOperationException("database down")));
    }

    [Fact]
    public async Task With_nothing_cached_a_failing_load_is_thrown()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Get("never-loaded", _ => throw new InvalidOperationException("database down")));
    }

    [Fact]
    public async Task A_cancelled_request_does_not_get_the_old_value()
    {
        await Get("k", _ => Task.FromResult(42));
        _clock.Advance(TimeSpan.FromSeconds(60));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Get("k", _ => throw new OperationCanceledException(cts.Token), cts.Token));
    }

    [Fact]
    public async Task Each_key_has_its_own_value()
    {
        Assert.Equal(1, await Get("a", _ => Task.FromResult(1)));
        Assert.Equal(2, await Get("b", _ => Task.FromResult(2)));
        Assert.Equal(1, await Get("a", _ => Task.FromResult(99)));
    }

    [Fact]
    public async Task Many_requests_for_one_key_at_once_load_it_only_once()
    {
        var loads = 0;
        var release = new TaskCompletionSource();

        async Task<int> Load(CancellationToken _)
        {
            Interlocked.Increment(ref loads);
            await release.Task;
            return 7;
        }

        var requests = Enumerable.Range(0, 20).Select(_ => Get("k", Load)).ToList();
        await Task.Delay(100);
        release.SetResult();
        var results = await Task.WhenAll(requests);

        Assert.All(results, r => Assert.Equal(7, r));
        Assert.Equal(1, loads);
    }
}
