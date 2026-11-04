using CoreApi.Common.Results;
using CoreApi.Entities;

namespace CoreApi.Repositories.Abstractions;

/// <summary>Not an <see cref="IRepository{TEntity,TKey}"/>: the key is two columns (user and project).</summary>
public interface IProjectMemberRepository
{
    /// <summary>The membership with its role loaded, or null.</summary>
    Task<ProjectMember?> GetAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Members of a project with their user and role, ordered by username.</summary>
    Task<PagedResult<ProjectMember>> GetPagedByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Every project the user belongs to, with project and role, ordered by project name.</summary>
    Task<IReadOnlyList<ProjectMember>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ProjectMember> CreateAsync(ProjectMember member, CancellationToken cancellationToken = default);
    Task DeleteAsync(ProjectMember member, CancellationToken cancellationToken = default);
}
