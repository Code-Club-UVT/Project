using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PlatformKit.Tests.Support;

/// <summary>An ES256 key pair like the one identity signs with, its JWKS and tokens signed with it.</summary>
public sealed class TestKey : IDisposable
{
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public TestKey(string kid = "key-1")
    {
        Kid = kid;
        SigningKey = new ECDsaSecurityKey(_ecdsa) { KeyId = kid };
    }

    public string Kid { get; }
    public ECDsaSecurityKey SigningKey { get; }

    /// <summary>The public key as published in the JWKS.</summary>
    public string PublicJwk()
    {
        var parameters = _ecdsa.ExportParameters(false);
        var x = Base64UrlEncoder.Encode(parameters.Q.X!);
        var y = Base64UrlEncoder.Encode(parameters.Q.Y!);
        return "{\"kty\":\"EC\",\"crv\":\"P-256\",\"x\":\"" + x + "\",\"y\":\"" + y + "\",\"kid\":\"" + Kid + "\",\"use\":\"sig\",\"alg\":\"ES256\"}";
    }

    public static string Jwks(params TestKey[] keys) => "{\"keys\":[" + string.Join(",", keys.Select(k => k.PublicJwk())) + "]}";

    /// <summary>A token like identity's. A negative <paramref name="expiresIn"/> gives one that expired already.</summary>
    public string Token(
        string issuer = "identity",
        string audience = "core",
        TimeSpan? expiresIn = null,
        SigningCredentials? credentials = null,
        bool unsigned = false)
    {
        var now = DateTime.UtcNow;
        var lifetime = expiresIn ?? TimeSpan.FromMinutes(5);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            NotBefore = lifetime > TimeSpan.Zero ? now : now.AddHours(-2),
            Expires = now.Add(lifetime),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["username"] = "tester",
                ["role"] = "Admin"
            }
        };

        if (!unsigned)
        {
            descriptor.SigningCredentials = credentials ?? new SigningCredentials(SigningKey, SecurityAlgorithms.EcdsaSha256);
        }

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public void Dispose() => _ecdsa.Dispose();
}
