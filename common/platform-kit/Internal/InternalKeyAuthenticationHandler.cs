using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PlatformKit.Internal;

/// <summary>Authenticates another service by the shared key in <c>X-Internal-Key</c>; the matching caller name becomes <c>sub</c>.</summary>
internal sealed class InternalKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<InternalAuthOptions> internalOptions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(InternalAuth.HeaderName, out var provided) || provided.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Compare fixed-length hashes in constant time, so neither the content nor the length of a key leaks through timing.
        // Every caller is compared (no early exit), so the timing does not tell which caller matched either.
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided.ToString()));
        string? caller = null;
        foreach (var (name, key) in internalOptions.Value.Callers)
        {
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            if (CryptographicOperations.FixedTimeEquals(expectedHash, providedHash))
            {
                caller = name;
            }
        }

        if (caller is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal key."));
        }

        var identity = new ClaimsIdentity([new Claim("sub", caller)], InternalAuth.SchemeName, "sub", "role");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), InternalAuth.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
