using Identity.Models.Requests;
using Identity.Services.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformKit.Internal;
using PlatformKit.Web;

namespace Identity.Controllers;

/// <summary>
/// Called by the gateway only (its <c>X-Internal-Key</c>). It lives outside <c>/api</c>, so the gateway never forwards
/// browser requests to it, and this API must not be reachable from outside except through the gateway.
/// </summary>
[Route("internal/project-tokens")]
[Authorize(AuthenticationSchemes = InternalAuth.SchemeName)]
public class InternalProjectTokensController(IProjectTokenService projectTokenService) : ResultControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Issue(ProjectTokenRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await projectTokenService.IssueAsync(request.UserId, request.Slug, cancellationToken));
    }
}
