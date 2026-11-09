using System.Security.Cryptography;
using Identity.Data.CoreRead;
using Identity.Entities;
using Identity.Mappings;
using Identity.Options;
using Identity.Services.Tokens;
using Identity.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Identity.Tests;

public class TokenTests
{
    private readonly IdentityFixture _f = new();

    private static User SomeUser() => new() { Id = Guid.NewGuid(), Username = "ada", Email = "ada@example.org" };

    private static JsonWebToken Read(string token) => new JsonWebTokenHandler().ReadJsonWebToken(token);

    [Fact]
    public void The_access_token_is_for_the_gateway_and_carries_who_the_user_is()
    {
        var user = SomeUser();

        var token = _f.Tokens.CreateAccessToken(user);

        var jwt = Read(token.Token);
        Assert.Equal("identity", jwt.Issuer);
        Assert.Equal(["gateway"], jwt.Audiences.ToArray());
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("ada@example.org", jwt.GetClaim("email").Value);
        Assert.Equal("ada", jwt.GetClaim("username").Value);
        Assert.False(jwt.TryGetClaim("role", out _));
        Assert.Equal(_f.Clock.UtcNow.AddMinutes(15), token.ExpiresAt);
        Assert.Equal(_f.Clock.UtcNow.AddMinutes(15), new DateTimeOffset(jwt.ValidTo).UtcDateTime);
    }

    [Fact]
    public void A_project_token_is_for_that_project_only_and_carries_the_role_but_no_email()
    {
        var user = SomeUser();

        var token = _f.Tokens.CreateProjectToken(user, "mock", "Editor");

        var jwt = Read(token.Token);
        Assert.Equal(["mock"], jwt.Audiences.ToArray());
        Assert.Equal("Editor", jwt.GetClaim("role").Value);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.False(jwt.TryGetClaim("email", out _));
        Assert.Equal(_f.Clock.UtcNow.AddMinutes(5), token.ExpiresAt);
    }

    [Fact]
    public async Task Tokens_are_signed_with_es256_and_verify_against_the_published_key()
    {
        var token = _f.Tokens.CreateAccessToken(SomeUser());

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = "identity",
            ValidAudience = "gateway",
            IssuerSigningKey = _f.SigningKey.SecurityKey,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            ValidateLifetime = false
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(_f.SigningKey.SecurityKey.KeyId, Read(token.Token).Kid);
    }

    [Fact]
    public void Every_token_has_its_own_id()
    {
        var user = SomeUser();

        var a = Read(_f.Tokens.CreateAccessToken(user).Token).Id;
        var b = Read(_f.Tokens.CreateAccessToken(user).Token).Id;

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Random_tokens_are_long_unique_and_url_safe()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => _f.Tokens.GenerateToken()).ToList();

        Assert.Equal(50, tokens.Distinct().Count());
        Assert.All(tokens, t =>
        {
            Assert.Equal(43, t.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", t);
        });
    }

    [Fact]
    public void The_hash_is_a_stable_64_character_hex_string_that_differs_per_token()
    {
        var hash = _f.Tokens.Hash("abc");

        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
        Assert.Equal(hash, _f.Tokens.Hash("abc"));
        Assert.NotEqual(hash, _f.Tokens.Hash("abd"));
    }

    // ---- the signing key

    private static JwtOptions JwtWith(string? pem) => new() { Issuer = "identity", Audience = "gateway", PrivateKeyPem = pem };

    private static SigningKeyProvider Provider(string? pem, string environment = "Development")
        => new(Opts.Of(JwtWith(pem)), new TestEnvironment(environment), NullLogger<SigningKeyProvider>.Instance);

    [Fact]
    public void Development_without_a_key_makes_a_temporary_one_and_publishes_its_public_half()
    {
        using var provider = Provider(null);

        var jwk = Assert.Single(provider.Jwks.Keys);
        Assert.Equal(("EC", "P-256", "sig", "ES256"), (jwk.Kty, jwk.Crv, jwk.Use, jwk.Alg));
        Assert.Equal(provider.SecurityKey.KeyId, jwk.Kid);
        Assert.NotEmpty(jwk.X);
        Assert.NotEmpty(jwk.Y);
    }

    [Fact]
    public void Outside_development_a_missing_key_stops_the_app()
    {
        Assert.Throws<InvalidOperationException>(() => Provider(null, "Production"));
        Assert.Throws<InvalidOperationException>(() => Provider("   ", "Production"));
    }

    [Fact]
    public void A_pem_key_is_used_and_the_key_id_is_stable()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ecdsa.ExportPkcs8PrivateKeyPem();

        using var one = Provider(pem, "Production");
        using var two = Provider(pem, "Production");

        Assert.Equal(one.SecurityKey.KeyId, two.SecurityKey.KeyId);
        Assert.Equal(16, one.SecurityKey.KeyId.Length);
    }

    [Fact]
    public void A_pem_in_one_line_with_escaped_new_lines_is_accepted()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var oneLine = ecdsa.ExportPkcs8PrivateKeyPem().Replace("\r", string.Empty).Replace("\n", "\\n");

        using var provider = Provider(oneLine, "Production");

        Assert.NotNull(provider.SecurityKey);
    }

    [Fact]
    public void A_key_that_is_not_p256_is_refused()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        Assert.Throws<InvalidOperationException>(() => Provider(p384.ExportPkcs8PrivateKeyPem(), "Production"));
    }

    // ---- project tokens

    private sealed class FakeAccessReader(ProjectAccess access) : IProjectAccessReader
    {
        public string? AskedSlug { get; private set; }

        public Task<ProjectAccess> GetAsync(Guid userId, string slug, CancellationToken cancellationToken = default)
        {
            AskedSlug = slug;
            return Task.FromResult(access);
        }
    }

    private ProjectTokenService ProjectTokens(ProjectAccess access, out FakeAccessReader reader)
    {
        reader = new FakeAccessReader(access);
        return new ProjectTokenService(_f.Users, reader, _f.Tokens);
    }

    [Fact]
    public async Task A_member_gets_a_token_with_the_role_and_the_project_as_audience()
    {
        var user = _f.AddUser("ada");
        var service = ProjectTokens(new ProjectAccess(ProjectAccessStatus.Granted, "mock", "Viewer"), out var reader);

        var result = await service.IssueAsync(user.Id, "  MOCK ");

        Assert.True(result.IsSuccess);
        Assert.Equal("mock", reader.AskedSlug);
        Assert.Equal("Viewer", result.Value.RoleName);
        var jwt = Read(result.Value.Token);
        Assert.Equal(["mock"], jwt.Audiences.ToArray());
        Assert.Equal("Viewer", jwt.GetClaim("role").Value);
    }

    [Fact]
    public async Task An_unknown_or_disabled_user_gets_no_token()
    {
        var off = _f.AddUser("off", active: false);
        var service = ProjectTokens(new ProjectAccess(ProjectAccessStatus.Granted, "mock", "Viewer"), out _);

        Assert.Same(TokenErrors.UserNotFound, (await service.IssueAsync(Guid.NewGuid(), "mock")).Error);
        Assert.Same(TokenErrors.UserNotFound, (await service.IssueAsync(off.Id, "mock")).Error);
    }

    [Fact]
    public async Task A_missing_project_and_a_missing_membership_are_told_apart()
    {
        var user = _f.AddUser("ada");

        var missing = await ProjectTokens(new ProjectAccess(ProjectAccessStatus.ProjectNotFound), out _).IssueAsync(user.Id, "nope");
        var notMember = await ProjectTokens(new ProjectAccess(ProjectAccessStatus.NotMember), out _).IssueAsync(user.Id, "mock");

        Assert.Same(TokenErrors.ProjectNotFound, missing.Error);
        Assert.Same(TokenErrors.NotMember, notMember.Error);
    }

    [Fact]
    public void A_user_maps_to_a_response_without_the_password_hash()
    {
        var user = SomeUser();
        user.PasswordHash = "secret-hash";
        user.EmailConfirmed = true;

        var response = user.ToResponse();

        Assert.Equal((user.Id, "ada", "ada@example.org", true), (response.Id, response.Username, response.Email, response.EmailConfirmed));
        Assert.DoesNotContain("secret-hash", System.Text.Json.JsonSerializer.Serialize(response));
    }
}
