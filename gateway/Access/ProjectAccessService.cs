using Gateway.Data;
using Gateway.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gateway.Access;

/// <summary>What the database says about one user in one project.</summary>
/// <param name="UserExists">The user row exists.</param>
/// <param name="UserActive">The account is not disabled.</param>
/// <param name="RoleId">The user's role in the project, or null when they are not a member.</param>
public sealed record AccessDecision(bool UserExists, bool UserActive, Guid? RoleId);

public interface IProjectAccessService
{
    Task<AccessDecision> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken);
}

/// <summary>Reads (and caches) the user's membership. The core API checks again when it issues the token.</summary>
public sealed class ProjectAccessService(
    IDbContextFactory<GatewayDbContext> dbFactory,
    TtlCache cache,
    IOptions<RoutingOptions> options) : IProjectAccessService
{
    public Task<AccessDecision> GetAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        return cache.GetOrLoadAsync(
            $"access:{userId:N}:{projectId:N}",
            TimeSpan.FromSeconds(settings.AccessCacheSeconds),
            TimeSpan.FromSeconds(settings.StaleGraceSeconds),
            ct => LoadAsync(userId, projectId, ct),
            cancellationToken);
    }

    private async Task<AccessDecision> LoadAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new AccessDecision(false, false, null);
        }

        var roleId = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.ProjectId == projectId)
            .Select(m => (Guid?)m.ProjectRoleId)
            .FirstOrDefaultAsync(cancellationToken);

        return new AccessDecision(true, user.IsActive, roleId);
    }
}
