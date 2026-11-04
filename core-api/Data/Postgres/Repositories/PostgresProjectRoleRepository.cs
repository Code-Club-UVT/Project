using CoreApi.Common.Results;
using CoreApi.Entities;
using CoreApi.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CoreApi.Data.Postgres.Repositories;

public class PostgresProjectRoleRepository(PostgresDbContext context)
    : PostgresRepository<ProjectRole, Guid>(context), IProjectRoleRepository
{
    private readonly PostgresDbContext _context = context;

    public async Task<PagedResult<ProjectRole>> GetPagedByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = Set.Where(r => r.ProjectId == projectId);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectRole>(items, page, pageSize, totalCount);
    }

    public Task<ProjectRole?> GetByNameAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
    {
        return Set.FirstOrDefaultAsync(r => r.ProjectId == projectId && r.Name == name, cancellationToken);
    }

    public Task<bool> HasMembersAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return _context.ProjectMembers.AnyAsync(m => m.ProjectRoleId == roleId, cancellationToken);
    }
}
