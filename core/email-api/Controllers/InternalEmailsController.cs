using EmailApi.Models;
using EmailApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformKit.Internal;
using PlatformKit.Web;

namespace EmailApi.Controllers;

/// <summary>Called by other services only (their <c>X-Internal-Key</c>). The email service has no gateway route at all.</summary>
[Route("internal/emails")]
[Authorize(AuthenticationSchemes = InternalAuth.SchemeName)]
public class InternalEmailsController(IEmailService emailService) : ResultControllerBase
{
    /// <summary>204 when sent (or already sent before), 400 for an unknown template or bad data, 500 when delivery failed.</summary>
    [HttpPost]
    public async Task<IActionResult> Send(SendEmailRequest request, CancellationToken cancellationToken)
    {
        return ToActionResult(await emailService.SendAsync(request, User.Identity!.Name!, cancellationToken));
    }
}
