using CoreApi.Entities;

namespace CoreApi.Services.Tokens;

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    /// <summary>Creates an opaque, cryptographically random token (for refresh tokens and emailed links).</summary>
    string GenerateToken();

    /// <summary>SHA-256 (hex) of a token; this is what gets stored.</summary>
    string Hash(string token);
}
