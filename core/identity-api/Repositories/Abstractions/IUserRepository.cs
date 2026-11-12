using Identity.Entities;

namespace Identity.Repositories.Abstractions;

public interface IUserRepository : IRepository<User, Guid>
{
    /// <summary>Finds a user by email when the identifier contains '@', otherwise by username.</summary>
    Task<User?> GetByIdentifierAsync(string identifier, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}
