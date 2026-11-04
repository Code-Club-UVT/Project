using CoreApi.Common.Results;
using CoreApi.Entities;

namespace CoreApi.Repositories.Abstractions;

public interface IUserRepository : IRepository<User, Guid>
{
    /// <summary>Finds a user (with profile) by email when the identifier contains '@', otherwise by username.</summary>
    Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<PagedResult<User>> GetPagedWithProfileAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
