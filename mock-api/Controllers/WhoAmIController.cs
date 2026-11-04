using GatewayAuth.RequestContext;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MockApi.Controllers;

/// <summary>Echoes what this API knows about the caller, to check the whole chain (browser, proxy, gateway, project token).</summary>
[ApiController]
[Route("api/whoami")]
public sealed class WhoAmIController(IRequestContext context) : ControllerBase
{
    /// <summary>Any user with access to this project.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        context.UserId,
        context.Username,
        context.Role,
        context.ProjectSlug,
        context.CorrelationId,
        context.ClientIp
    });

    /// <summary>Only users whose role in this project is Admin; for testing role checks.</summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetAdmin() => Ok(new { message = "You are an Admin in this project.", context.Username });
}
