using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Identity.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The authenticated user has no valid 'sub' claim.");
    }
}
