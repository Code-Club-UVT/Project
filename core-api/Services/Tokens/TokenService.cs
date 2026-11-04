using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using CoreApi.Entities;
using CoreApi.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.Services.Tokens;

public class TokenService(IOptions<JwtOptions> options, ISigningKeyProvider signingKey, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials = new(signingKey.SecurityKey, SecurityAlgorithms.EcdsaSha256);

    public AccessToken CreateAccessToken(User user)
    {
        return Create(_jwt.Audience, _jwt.AccessTokenMinutes, new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email,
            ["username"] = user.Username,
            ["role"] = user.Role.ToString()
        });
    }

    public AccessToken CreateProjectToken(User user, Project project, ProjectRole role)
    {
        return Create(project.Slug, _jwt.ProjectTokenMinutes, new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            ["username"] = user.Username,
            ["role"] = role.Name
        });
    }

    public string GenerateToken()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
    }

    public string Hash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private AccessToken Create(string audience, int minutes, Dictionary<string, object> claims)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(minutes);

        claims[JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = audience,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _credentials,
            Claims = claims
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
