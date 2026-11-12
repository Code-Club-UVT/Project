using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using PlatformKit.Internal;
using Xunit;

namespace PlatformKit.Tests;

public class InternalKeyAuthenticationTests
{
    private const string GatewayKey = "gateway-key-0123456789-0123456789-abcdef";
    private const string IdentityKey = "identity-key-0123456789-0123456789-abcde";

    private sealed class StaticMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();
        public AuthenticationSchemeOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(Dictionary<string, string> callers, params string[] headerValues)
    {
        var handler = new InternalKeyAuthenticationHandler(
            new StaticMonitor(),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            Options.Create(new InternalAuthOptions { Callers = callers }));

        var context = new DefaultHttpContext();
        if (headerValues.Length > 0)
        {
            context.Request.Headers[InternalAuth.HeaderName] = new StringValues(headerValues);
        }

        await handler.InitializeAsync(
            new AuthenticationScheme(InternalAuth.SchemeName, null, typeof(InternalKeyAuthenticationHandler)),
            context);

        return await handler.AuthenticateAsync();
    }

    private static Dictionary<string, string> Callers() => new()
    {
        ["gateway"] = GatewayKey,
        ["identity"] = IdentityKey
    };

    [Fact]
    public async Task The_right_key_authenticates_the_caller_by_name()
    {
        var result = await AuthenticateAsync(Callers(), GatewayKey);

        Assert.True(result.Succeeded);
        Assert.Equal("gateway", result.Principal!.FindFirst("sub")!.Value);
    }

    [Fact]
    public async Task The_key_of_another_caller_gives_that_callers_name()
    {
        var result = await AuthenticateAsync(Callers(), IdentityKey);

        Assert.True(result.Succeeded);
        Assert.Equal("identity", result.Principal!.FindFirst("sub")!.Value);
    }

    [Fact]
    public async Task A_wrong_key_fails()
    {
        var result = await AuthenticateAsync(Callers(), "not-a-known-key-not-a-known-key-0000");

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task A_key_that_differs_by_one_character_fails()
    {
        var almost = GatewayKey[..^1] + (GatewayKey[^1] == 'f' ? 'e' : 'f');

        var result = await AuthenticateAsync(Callers(), almost);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task No_header_is_no_result_so_another_scheme_may_answer()
    {
        var result = await AuthenticateAsync(Callers());

        Assert.False(result.Succeeded);
        Assert.Null(result.Failure);
        Assert.True(result.None);
    }

    [Fact]
    public async Task Two_key_headers_are_no_result()
    {
        var result = await AuthenticateAsync(Callers(), GatewayKey, IdentityKey);

        Assert.True(result.None);
    }

    [Fact]
    public async Task With_no_callers_configured_every_key_fails()
    {
        var result = await AuthenticateAsync([], GatewayKey);

        Assert.False(result.Succeeded);
    }
}
