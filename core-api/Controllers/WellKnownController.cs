using CoreApi.Services.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

/// <summary>Public keys for validating every token this API signs. Downstream APIs and the gateway fetch it.</summary>
[ApiController]
[AllowAnonymous]
[Route(".well-known")]
public class WellKnownController(ISigningKeyProvider signingKey) : ControllerBase
{
    [HttpGet("jwks.json")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public IActionResult GetJwks()
    {
        return Ok(signingKey.Jwks);
    }
}
