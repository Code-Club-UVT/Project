using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PlatformKit.Tests.Support;
using Xunit;

namespace PlatformKit.Tests;

/// <summary>The real validation setup of every API, against a real (local) JWKS endpoint.</summary>
public sealed class TokenValidationTests : IAsyncLifetime
{
    private readonly TestKey _identityKey = new("identity-key");
    private JwksServer _server = null!;
    private ServiceProvider _provider = null!;
    private TokenValidationParameters _parameters = null!;

    public async Task InitializeAsync()
    {
        _server = await JwksServer.StartAsync(() => TestKey.Jwks(_identityKey));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformAuth:Issuer"] = "identity",
            ["PlatformAuth:Audience"] = "core",
            ["PlatformAuth:JwksUrl"] = _server.Url,
            ["PlatformAuth:AllowInsecureJwksUrl"] = "true"
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPlatformAuthentication(config);
        _provider = services.BuildServiceProvider();

        _parameters = _provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _server.DisposeAsync();
        _identityKey.Dispose();
    }

    private Task<TokenValidationResult> ValidateAsync(string token) => new JsonWebTokenHandler().ValidateTokenAsync(token, _parameters);

    [Fact]
    public async Task A_token_from_identity_for_this_api_is_accepted()
    {
        var result = await ValidateAsync(_identityKey.Token());

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("tester", result.ClaimsIdentity.FindFirst("username")!.Value);
        Assert.Equal("Admin", result.ClaimsIdentity.FindFirst("role")!.Value);
    }

    [Fact]
    public async Task A_token_for_another_project_is_refused()
    {
        Assert.False((await ValidateAsync(_identityKey.Token(audience: "mock"))).IsValid);
    }

    [Fact]
    public async Task The_edge_token_audience_is_refused()
    {
        Assert.False((await ValidateAsync(_identityKey.Token(audience: "gateway"))).IsValid);
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_refused()
    {
        Assert.False((await ValidateAsync(_identityKey.Token(issuer: "someone-else"))).IsValid);
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        Assert.False((await ValidateAsync(_identityKey.Token(expiresIn: TimeSpan.FromMinutes(-10)))).IsValid);
    }

    [Fact]
    public async Task A_token_signed_with_another_key_under_the_same_kid_is_refused()
    {
        using var attacker = new TestKey("identity-key");

        Assert.False((await ValidateAsync(attacker.Token())).IsValid);
    }

    [Fact]
    public async Task A_token_with_an_unknown_kid_is_refused()
    {
        using var other = new TestKey("not-published");

        Assert.False((await ValidateAsync(other.Token())).IsValid);
    }

    [Fact]
    public async Task An_unsigned_token_is_refused()
    {
        Assert.False((await ValidateAsync(_identityKey.Token(unsigned: true))).IsValid);
    }

    [Fact]
    public async Task A_symmetric_hs256_token_is_refused()
    {
        var secret = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)) { KeyId = "identity-key" };
        var token = _identityKey.Token(credentials: new SigningCredentials(secret, SecurityAlgorithms.HmacSha256));

        Assert.False((await ValidateAsync(token)).IsValid);
    }

    [Fact]
    public async Task An_rs256_token_is_refused_because_only_es256_is_allowed()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "identity-key" };
        var token = _identityKey.Token(credentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        Assert.False((await ValidateAsync(token)).IsValid);
    }

    [Fact]
    public async Task A_token_with_a_changed_payload_is_refused()
    {
        var parts = _identityKey.Token().Split('.');
        var payload = Base64UrlEncoder.Decode(parts[1]).Replace("\"Admin\"", "\"Boss\"");
        var tampered = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        Assert.False((await ValidateAsync(tampered)).IsValid);
    }
}
