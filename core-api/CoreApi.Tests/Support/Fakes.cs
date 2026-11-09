using CoreApi.Entities;
using CoreApi.Repositories.Abstractions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PlatformKit.Results;

namespace CoreApi.Tests.Support;

/// <summary>A clock the test moves by hand.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FakeTimeProvider(DateTimeOffset? start = null)
    {
        _now = start ?? new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public DateTime UtcNow => _now.UtcDateTime;

    public void Advance(TimeSpan by) => _now += by;
}

public sealed class TestEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "tests";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.FromResult(1);
    }
}

public abstract class FakeRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : class
    where TKey : notnull
{
    public List<TEntity> Items { get; } = [];

    public Task<PagedResult<TEntity>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<TEntity>(Items.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, Items.Count));

    public virtual Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(item => Equals(KeyOf(item), id)));

    public Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Items.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public virtual Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Items.Remove(entity);
        return Task.CompletedTask;
    }

    protected abstract TKey KeyOf(TEntity entity);

    protected static PagedResult<T> Page<T>(IEnumerable<T> ordered, int page, int pageSize)
    {
        var all = ordered.ToList();
        return new PagedResult<T>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count);
    }
}

public sealed class FakeProfileRepository : FakeRepository<UserProfile, Guid>, IUserProfileRepository
{
    protected override Guid KeyOf(UserProfile entity) => entity.UserId;

    public Task<PagedResult<UserProfile>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(Page(Items.OrderBy(p => p.Username, StringComparer.OrdinalIgnoreCase), page, pageSize));
}

public sealed class FakeProjectRepository : FakeRepository<Project, Guid>, IProjectRepository
{
    protected override Guid KeyOf(Project entity) => entity.Id;

    public Task<Project?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(p => p.Slug == slug));

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(p => p.Slug == slug));

    public Task<PagedResult<Project>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(Page(Items.OrderBy(p => p.Slug, StringComparer.Ordinal), page, pageSize));

    public Task<IReadOnlyList<Project>> GetActiveWithDefaultRoleAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Project>>(Items.Where(p => p.IsActive && p.DefaultRoleId is not null).ToList());
}

public sealed class FakeRoleRepository(FakeMemberRepository members) : FakeRepository<ProjectRole, Guid>, IProjectRoleRepository
{
    protected override Guid KeyOf(ProjectRole entity) => entity.Id;

    public Task<PagedResult<ProjectRole>> GetPagedByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
        => Task.FromResult(Page(Items.Where(r => r.ProjectId == projectId).OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase), page, pageSize));

    /// <summary>The database column is case-insensitive (citext), so is this.</summary>
    public Task<ProjectRole?> GetByNameAsync(Guid projectId, string name, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(r => r.ProjectId == projectId && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> HasMembersAsync(Guid roleId, CancellationToken cancellationToken = default)
        => Task.FromResult(members.Items.Any(m => m.ProjectRoleId == roleId));
}

public sealed class FakeMemberRepository : IProjectMemberRepository
{
    public List<ProjectMember> Items { get; } = [];

    /// <summary>Set by the fixture so the memberships come back with their project, role and user, as the real repository loads them.</summary>
    public Func<ProjectMember, ProjectMember> Load { get; set; } = m => m;

    public Task<ProjectMember?> GetAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Where(m => m.ProjectId == projectId && m.UserId == userId).Select(Load).FirstOrDefault());

    public Task<PagedResult<ProjectMember>> GetPagedByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var all = Items.Where(m => m.ProjectId == projectId).Select(Load).OrderBy(m => m.User.Username, StringComparer.OrdinalIgnoreCase).ToList();
        return Task.FromResult(new PagedResult<ProjectMember>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count));
    }

    public Task<IReadOnlyList<ProjectMember>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProjectMember>>(Items.Where(m => m.UserId == userId).Select(Load).OrderBy(m => m.Project.Name, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<IReadOnlyList<Guid>> GetProjectIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Guid>>(Items.Where(m => m.UserId == userId).Select(m => m.ProjectId).ToList());

    public Task<ProjectMember> CreateAsync(ProjectMember member, CancellationToken cancellationToken = default)
    {
        Items.Add(member);
        return Task.FromResult(member);
    }

    public Task DeleteAsync(ProjectMember member, CancellationToken cancellationToken = default)
    {
        Items.Remove(member);
        return Task.CompletedTask;
    }
}

public sealed class FakeTabRepository(FakeMemberRepository members) : IUserTabRepository
{
    public List<UserTab> Items { get; } = [];

    public Task<IReadOnlyList<UserTab>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tabs = Items.Where(t => t.UserId == userId).OrderBy(t => t.Position).ToList();
        foreach (var tab in tabs)
        {
            tab.Member = members.Items.Where(m => m.UserId == tab.UserId && m.ProjectId == tab.ProjectId).Select(members.Load).First();
        }

        return Task.FromResult<IReadOnlyList<UserTab>>(tabs);
    }

    public Task<IReadOnlyList<UserTab>> GetTrackedByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserTab>>(Items.Where(t => t.UserId == userId).ToList());

    public Task CreateAsync(UserTab tab, CancellationToken cancellationToken = default)
    {
        Items.Add(tab);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(UserTab tab, CancellationToken cancellationToken = default)
    {
        Items.Remove(tab);
        return Task.CompletedTask;
    }
}
