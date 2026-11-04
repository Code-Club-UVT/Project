using System.Buffers.Text;
using System.Security.Cryptography;
using CoreApi.Models.Responses;
using CoreApi.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.Services.Tokens;

public sealed class SigningKeyProvider : ISigningKeyProvider, IDisposable
{
    private readonly ECDsa _ecdsa = ECDsa.Create();

    public SigningKeyProvider(IOptions<JwtOptions> options, IHostEnvironment environment, ILogger<SigningKeyProvider> logger)
    {
        var pem = options.Value.PrivateKeyPem;
        if (string.IsNullOrWhiteSpace(pem))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Jwt:PrivateKeyPem is not set. Provide an ECDSA P-256 private key in PEM (for example with the Jwt__PrivateKeyPem environment variable).");
            }

            _ecdsa.GenerateKey(ECCurve.NamedCurves.nistP256);
            logger.LogWarning("Jwt:PrivateKeyPem is not set: using a temporary signing key. Issued tokens stop working when the app restarts.");
        }
        else
        {
            _ecdsa.ImportFromPem(pem.Replace("\\n", "\n"));
            if (_ecdsa.KeySize != 256)
            {
                throw new InvalidOperationException("Jwt:PrivateKeyPem must be an ECDSA P-256 key (ES256).");
            }
        }

        var keyId = Base64Url.EncodeToString(SHA256.HashData(_ecdsa.ExportSubjectPublicKeyInfo()))[..16];
        SecurityKey = new ECDsaSecurityKey(_ecdsa) { KeyId = keyId };

        var point = _ecdsa.ExportParameters(includePrivateParameters: false).Q;
        Jwks = new JwksResponse([
            new JsonWebKeyResponse(
                Kty: "EC",
                Crv: "P-256",
                X: Base64Url.EncodeToString(point.X!),
                Y: Base64Url.EncodeToString(point.Y!),
                Use: "sig",
                Alg: SecurityAlgorithms.EcdsaSha256,
                Kid: keyId)
        ]);
    }

    public SecurityKey SecurityKey { get; }
    public JwksResponse Jwks { get; }

    public void Dispose() => _ecdsa.Dispose();
}
