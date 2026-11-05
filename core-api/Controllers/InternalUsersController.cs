using CoreApi.Models.Requests;
using CoreApi.Services.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformKit.Internal;
using PlatformKit.Web;

namespace CoreApi.Controllers;

/// <summary>
/// Called by identity only (its outbox, with its <c>X-Internal-Key</c>). Outside <c>/api</c>, so the gateway never routes
/// it. Both endpoints are idempotent, because a delivery can be repeated.
/// </summary>
[Route("internal/users")]
[Authorize(AuthenticationSchemes = InternalAuth.SchemeName)]
public class InternalUsersController(IUserProvisioningService provisioning) : ResultControllerBase
{
    /// <summary>An account was registered: create (or refresh) its profile.</summary>
    [HttpPost]
    public async Task<IActionResult> Upsert(InternalUserRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await provisioning.UpsertAsync(request, cancellationToken));
    }

    /// <summary>The email was confirmed: grant the projects' default roles. 404 while the profile does not exist yet.</summary>
    [HttpPost("{userId:guid}/confirmed")]
    public async Task<IActionResult> Confirmed(Guid userId, CancellationToken cancellationToken)
    {
        return ToActionResult(await provisioning.ConfirmedAsync(userId, cancellationToken));
    }
}
