using PlatformKit.Results;
using CoreApi.Entities;
using CoreApi.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CoreApi.Data.Postgres.Repositories;

public class PostgresProjectMemberRepository(PostgresDbContext context) : IProjectMemberRepository
{
    private const int MaxPageSize = 100;

    private DbSet<ProjectMember> Set => context.ProjectMembers;

    public Task<ProjectMember?> GetAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        return Set.Include(m => m.Role).FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);
    }

    public async Task<PagedResult<ProjectMember>> GetPagedByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = Set.Where(m => m.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Role)
            .OrderBy(m => m.User.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectMember>(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<ProjectMember>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Set
            .AsNoTracking()
            .Include(m => m.Project)
            .Include(m => m.Role)
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Project.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetProjectIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Set.Where(m => m.UserId == userId).Select(m => m.ProjectId).ToListAsync(cancellationToken);
    }

    public async Task<ProjectMember> CreateAsync(ProjectMember member, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(member, cancellationToken);
        return member;
    }

    public Task DeleteAsync(ProjectMember member, CancellationToken cancellationToken = default)
    {
        Set.Remove(member);
        return Task.CompletedTask;
    }
}
