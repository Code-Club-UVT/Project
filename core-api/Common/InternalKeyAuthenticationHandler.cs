using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using CoreApi.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CoreApi.Common;

/// <summary>Authenticates the gateway with a shared secret in the <c>X-Internal-Key</c> header.</summary>
public class InternalKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<InternalOptions> internalOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "InternalKey";
    public const string HeaderName = "X-Internal-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || provided.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Compare fixed-length hashes in constant time, so neither the content nor the length of the key leaks through timing.
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(internalOptions.Value.ApiKey));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided.ToString()));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, providedHash))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal key."));
        }

        var identity = new ClaimsIdentity([new Claim("sub", "gateway")], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
