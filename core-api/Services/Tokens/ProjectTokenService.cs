using CoreApi.Common.Results;
using CoreApi.Enums;
using CoreApi.Models.Responses;
using CoreApi.Repositories.Abstractions;
using CoreApi.Services.Projects;
using CoreApi.Services.Users;

namespace CoreApi.Services.Tokens;

public class ProjectTokenService(
    IUserRepository users,
    IProjectRepository projects,
    IProjectMemberRepository members,
    ITokenService tokens) : IProjectTokenService
{
    public async Task<Result<ProjectTokenResponse>> IssueAsync(Guid userId, string slug, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return UserErrors.NotFound;
        }

        var project = await projects.GetBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken);
        if (project is null || !project.IsActive || project.TokenMode != ProjectTokenMode.Exchange)
        {
            return ProjectErrors.NotFound;
        }

        var member = await members.GetAsync(project.Id, user.Id, cancellationToken);
        if (member is null)
        {
            return ProjectErrors.NotMember;
        }

        var token = tokens.CreateProjectToken(user, project, member.Role);

        return Result.Success(new ProjectTokenResponse(token.Token, token.ExpiresAt, member.Role.Name));
    }
}
