using Identity.Data.CoreRead;
using Identity.Models.Responses;
using Identity.Repositories.Abstractions;
using PlatformKit.Results;

namespace Identity.Services.Tokens;

public class ProjectTokenService(
    IUserRepository users,
    IProjectAccessReader projectAccess,
    ITokenService tokens) : IProjectTokenService
{
    public async Task<Result<ProjectTokenResponse>> IssueAsync(Guid userId, string slug, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return TokenErrors.UserNotFound;
        }

        var access = await projectAccess.GetAsync(user.Id, slug.Trim().ToLowerInvariant(), cancellationToken);
        switch (access.Status)
        {
            case ProjectAccessStatus.ProjectNotFound:
                return TokenErrors.ProjectNotFound;
            case ProjectAccessStatus.NotMember:
                return TokenErrors.NotMember;
        }

        var token = tokens.CreateProjectToken(user, access.Slug!, access.RoleName!);

        return Result.Success(new ProjectTokenResponse(token.Token, token.ExpiresAt, access.RoleName!));
    }
}
