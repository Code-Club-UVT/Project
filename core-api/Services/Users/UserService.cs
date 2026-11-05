using CoreApi.Mappings;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using CoreApi.Repositories.Abstractions;
using PlatformKit.Results;

namespace CoreApi.Services.Users;

public class UserService(IUserProfileRepository profiles, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IUserService
{
    public async Task<Result<UserResponse>> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await profiles.GetByIdAsync(userId, cancellationToken);

        return profile is null ? UserErrors.NotFound : Result.Success(profile.ToResponse());
    }

    public async Task<Result<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await profiles.GetByIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return UserErrors.NotFound;
        }

        profile.FirstName = NullIfBlank(request.FirstName);
        profile.LastName = NullIfBlank(request.LastName);
        profile.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(profile.ToResponse());
    }

    public async Task<Result<PagedResult<UserResponse>>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await profiles.GetPagedAsync(page, pageSize, cancellationToken);
        var items = result.Items.Select(p => p.ToResponse()).ToList();

        return Result.Success(new PagedResult<UserResponse>(items, result.Page, result.PageSize, result.TotalCount));
    }

    internal static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
