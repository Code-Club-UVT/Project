using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PlatformKit.Auth;
using PlatformKit.Tests.Support;
using Xunit;

namespace PlatformKit.Tests;

public class JwksKeyStoreTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new();
    private readonly TestKey _first = new("key-1");
    private readonly TestKey _second = new("key-2");
    private string _json;
    private bool _failing;

    private readonly StubHandler _handler;
    private readonly JwksKeyStore _store;

    public JwksKeyStoreTests()
    {
        _json = TestKey.Jwks(_first);
        _handler = new StubHandler(_ => _failing
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_json) });

        _store = new JwksKeyStore(
            new HttpClient(_handler),
            Options.Create(new PlatformAuthOptions { JwksUrl = "http://identity/jwks", KeysRefreshMinutes = 60, UnknownKeyRefreshMinutes = 1 }),
            _clock,
            NullLogger<JwksKeyStore>.Instance);
    }

    public void Dispose()
    {
        _first.Dispose();
        _second.Dispose();
    }

    private static string[] Ids(IEnumerable<SecurityKey> keys) => keys.Select(k => k.KeyId).ToArray();

    [Fact]
    public void The_first_use_downloads_the_keys_and_returns_the_one_asked_for()
    {
        var keys = _store.GetKeys("key-1");

        Assert.Equal(["key-1"], Ids(keys));
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public void Known_keys_are_not_downloaded_again_within_the_refresh_time()
    {
        _store.GetKeys("key-1");
        _clock.Advance(TimeSpan.FromMinutes(30));

        _store.GetKeys("key-1");

        Assert.Single(_handler.Requests);
    }

    [Fact]
    public void The_keys_are_refreshed_after_the_refresh_time()
    {
        _store.GetKeys("key-1");
        _clock.Advance(TimeSpan.FromMinutes(61));

        _store.GetKeys("key-1");

        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public void A_token_without_a_kid_gets_every_key()
    {
        _json = TestKey.Jwks(_first, _second);

        var keys = _store.GetKeys(null);

        Assert.Equal(["key-1", "key-2"], Ids(keys).Order().ToArray());
    }

    [Fact]
    public void An_unknown_kid_does_not_download_again_straight_after_a_download()
    {
        _store.GetKeys("key-1");

        var keys = _store.GetKeys("made-up");

        Assert.Empty(keys);
        Assert.Single(_handler.Requests);
    }

    [Fact]
    public void An_unknown_kid_downloads_again_after_the_minimum_wait_so_a_rotated_key_works()
    {
        _store.GetKeys("key-1");
        _json = TestKey.Jwks(_first, _second);
        _clock.Advance(TimeSpan.FromMinutes(1));

        var keys = _store.GetKeys("key-2");

        Assert.Equal(["key-2"], Ids(keys));
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public void A_failed_download_gives_no_keys_and_does_not_throw()
    {
        _failing = true;

        var keys = _store.GetKeys("key-1");

        Assert.Empty(keys);
    }

    [Fact]
    public void After_a_failure_it_waits_a_few_seconds_and_then_recovers()
    {
        _failing = true;
        _store.GetKeys("key-1");

        _store.GetKeys("key-1");
        Assert.Single(_handler.Requests);

        _failing = false;
        _clock.Advance(TimeSpan.FromSeconds(6));
        var keys = _store.GetKeys("key-1");

        Assert.Equal(["key-1"], Ids(keys));
        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public void A_failed_refresh_keeps_the_keys_it_had()
    {
        _store.GetKeys("key-1");
        _failing = true;
        _clock.Advance(TimeSpan.FromMinutes(61));

        var keys = _store.GetKeys("key-1");

        Assert.Equal(["key-1"], Ids(keys));
    }

    [Fact]
    public void An_empty_key_set_keeps_the_keys_it_had()
    {
        _store.GetKeys("key-1");
        _json = "{\"keys\":[]}";
        _clock.Advance(TimeSpan.FromMinutes(61));

        var keys = _store.GetKeys("key-1");

        Assert.Equal(["key-1"], Ids(keys));
    }
}
