using Identity.Entities;

namespace Identity.Repositories.Abstractions;

public interface IRefreshTokenRepository : IRepository<RefreshToken, Guid>
{
    /// <summary>Finds a token by its hash, including its user.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
