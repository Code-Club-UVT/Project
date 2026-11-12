using Identity.Models.Responses;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Services.Tokens;

/// <summary>The one ECDSA key every token is signed with, and its public half as a JWKS.</summary>
public interface ISigningKeyProvider
{
    SecurityKey SecurityKey { get; }
    JwksResponse Jwks { get; }
}
