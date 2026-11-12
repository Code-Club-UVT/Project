using Identity.Entities;
using Identity.Enums;
using Identity.Repositories.Abstractions;
using Identity.Services.Outbox;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PlatformKit.Results;

namespace Identity.Tests.Support;

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

    /// <summary>Called after every save (the dispatcher tests wait on it).</summary>
    public Action? OnSaved { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        OnSaved?.Invoke();
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

    public Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        Items.Remove(entity);
        return Task.CompletedTask;
    }

    protected abstract TKey KeyOf(TEntity entity);
}

public sealed class FakeUserRepository : FakeRepository<User, Guid>, IUserRepository
{
    protected override Guid KeyOf(User entity) => entity.Id;

    public Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
        => identifier.Contains('@') ? GetByEmailAsync(identifier, cancellationToken) : Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Username, identifier, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));
}

public sealed class FakeRefreshTokenRepository(FakeUserRepository users) : FakeRepository<RefreshToken, Guid>, IRefreshTokenRepository
{
    protected override Guid KeyOf(RefreshToken entity) => entity.Id;

    /// <summary>Like the real repository, the token comes with its user.</summary>
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var token = Items.FirstOrDefault(t => t.TokenHash == tokenHash);
        token?.User = users.Items.First(u => u.Id == token.UserId);
        return Task.FromResult(token);
    }

    public Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(t => t.FamilyId == familyId && t.RevokedAt is null).ToList());

    public Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RefreshToken>>(Items.Where(t => t.UserId == userId && t.RevokedAt is null).ToList());
}

public sealed class FakeUserTokenRepository(FakeUserRepository users) : FakeRepository<UserToken, Guid>, IUserTokenRepository
{
    protected override Guid KeyOf(UserToken entity) => entity.Id;

    /// <summary>Like the real repository, the token comes with its user.</summary>
    public Task<UserToken?> GetByHashAsync(string tokenHash, UserTokenType type, CancellationToken cancellationToken = default)
    {
        var token = Items.FirstOrDefault(t => t.TokenHash == tokenHash && t.Type == type);
        token?.User = users.Items.First(u => u.Id == token.UserId);
        return Task.FromResult(token);
    }

    public Task<IReadOnlyList<UserToken>> GetUnusedAsync(Guid userId, UserTokenType type, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<UserToken>>(Items.Where(t => t.UserId == userId && t.Type == type && t.UsedAt is null).ToList());
}

public sealed class FakeOutboxRepository : FakeRepository<OutboxMessage, Guid>, IOutboxRepository
{
    protected override Guid KeyOf(OutboxMessage entity) => entity.Id;

    public Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DateTime now, DateTime leaseUntil, int batchSize, CancellationToken cancellationToken = default)
    {
        var due = Items.Where(m => m.FailedAt is null && m.NextAttemptAt <= now).OrderBy(m => m.CreatedAt).Take(batchSize).ToList();
        foreach (var message in due)
        {
            message.NextAttemptAt = leaseUntil;
        }

        return Task.FromResult<IReadOnlyList<OutboxMessage>>(due);
    }
}

/// <summary>Records what was queued instead of saving it.</summary>
public sealed class FakeOutbox : IOutbox
{
    public record Email(string Template, string To, IReadOnlyDictionary<string, string> Data);

    public List<(Guid UserId, string Username, string Email, string? FirstName, string? LastName)> Registered { get; } = [];
    public List<Guid> Confirmed { get; } = [];
    public List<Email> Emails { get; } = [];

    public Task UserRegisteredAsync(Guid userId, string username, string email, string? firstName, string? lastName, CancellationToken cancellationToken = default)
    {
        Registered.Add((userId, username, email, firstName, lastName));
        return Task.CompletedTask;
    }

    public Task UserConfirmedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Confirmed.Add(userId);
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(string template, string to, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
    {
        Emails.Add(new Email(template, to, data));
        return Task.CompletedTask;
    }
}
