using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using PlatformKit.Results;

namespace CoreApi.Services.UserTabs;

public interface IUserTabService
{
    /// <summary>The user's saved tabs, in order, without those whose project is inactive or has no frontend.</summary>
    Task<Result<IReadOnlyList<UserTabResponse>>> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the saved list. Projects the user is not a member of are dropped and a project listed twice counts once
    /// (the first position wins).
    /// </summary>
    Task<Result<IReadOnlyList<UserTabResponse>>> ReplaceAsync(Guid userId, SaveUserTabsRequest request, CancellationToken cancellationToken = default);
}
