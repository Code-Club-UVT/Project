using PlatformKit.Results;
using Identity.Models.Responses;

namespace Identity.Services.Tokens;

public interface IProjectTokenService
{
    /// <summary>Mints a project token for a user, after checking the user, the project and the membership again.</summary>
    Task<Result<ProjectTokenResponse>> IssueAsync(Guid userId, string slug, CancellationToken cancellationToken = default);
}
