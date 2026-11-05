using CoreApi.Models.Requests;
using PlatformKit.Results;

namespace CoreApi.Services.Users;

/// <summary>What identity tells the core API about accounts (its outbox). Both calls are idempotent: a retry changes nothing.</summary>
public interface IUserProvisioningService
{
    /// <summary>Creates the profile, or updates the read copy of username and email if it exists (the name is not overwritten).</summary>
    Task<Result> UpsertAsync(InternalUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The email was confirmed: grants the default role of every active project the user is not in yet. When the email
    /// is <c>Seed:AdminEmail</c>, the user gets <c>Admin</c> on the core project instead of its default role.
    /// 404 until the profile exists (identity delivers in order and retries).
    /// </summary>
    Task<Result> ConfirmedAsync(Guid userId, CancellationToken cancellationToken = default);
}
