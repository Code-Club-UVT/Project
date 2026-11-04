using CoreApi.Common;
using CoreApi.Models.Requests;
using CoreApi.Services.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

/// <summary>
/// Called by the gateway only (shared <c>X-Internal-Key</c>). It lives outside <c>/api</c>, so the gateway never
/// forwards browser requests to it, and the API must not be reachable from outside except through the gateway.
/// </summary>
[Route("internal/project-tokens")]
[Authorize(AuthenticationSchemes = InternalKeyAuthenticationHandler.SchemeName)]
public class InternalProjectTokensController(IProjectTokenService projectTokenService) : ResultControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Issue(ProjectTokenRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await projectTokenService.IssueAsync(request.UserId, request.Slug, cancellationToken));
    }
}
