using Microsoft.EntityFrameworkCore;

namespace Identity.Data.CoreRead;

public enum ProjectAccessStatus
{
    Granted,

    /// <summary>No such project, or it is inactive, or it does not take project tokens (PassThrough).</summary>
    ProjectNotFound,

    NotMember
}

public sealed record ProjectAccess(ProjectAccessStatus Status, string? Slug = null, string? RoleName = null);

public interface IProjectAccessReader
{
    /// <summary>The user's role in an active Exchange project, read from the core API's tables.</summary>
    Task<ProjectAccess> GetAsync(Guid userId, string slug, CancellationToken cancellationToken = default);
}

public class ProjectAccessReader(CoreReadDbContext context) : IProjectAccessReader
{
    private const string ExchangeMode = "Exchange";

    public async Task<ProjectAccess> GetAsync(Guid userId, string slug, CancellationToken cancellationToken = default)
    {
        var project = await context.Projects.AsNoTracking()
            .Where(p => p.Slug == slug && p.IsActive && p.TokenMode == ExchangeMode)
            .Select(p => new { p.Id, p.Slug })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
        {
            return new ProjectAccess(ProjectAccessStatus.ProjectNotFound);
        }

        var roleName = await (
                from m in context.ProjectMembers.AsNoTracking()
                join r in context.ProjectRoles.AsNoTracking() on m.ProjectRoleId equals r.Id
                where m.UserId == userId && m.ProjectId == project.Id
                select r.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return roleName is null
            ? new ProjectAccess(ProjectAccessStatus.NotMember)
            : new ProjectAccess(ProjectAccessStatus.Granted, project.Slug, roleName);
    }
}
