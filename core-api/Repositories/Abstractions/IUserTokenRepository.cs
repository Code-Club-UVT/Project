using CoreApi.Entities;
using CoreApi.Enums;

namespace CoreApi.Repositories.Abstractions;

public interface IUserTokenRepository : IRepository<UserToken, Guid>
{
    /// <summary>Finds a token by its hash and type, including its user.</summary>
    Task<UserToken?> GetByHashAsync(string tokenHash, UserTokenType type, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserToken>> GetUnusedAsync(Guid userId, UserTokenType type, CancellationToken cancellationToken = default);
}
