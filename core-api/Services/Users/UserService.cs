using CoreApi.Common.Results;
using CoreApi.Entities;
using CoreApi.Mappings;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;
using CoreApi.Repositories.Abstractions;

namespace CoreApi.Services.Users;

public class UserService(IUserRepository users, IUnitOfWork unitOfWork, TimeProvider timeProvider) : IUserService
{
    public async Task<Result<UserResponse>> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdWithProfileAsync(userId, cancellationToken);

        return user is null || !user.IsActive
            ? UserErrors.NotFound
            : Result.Success(user.ToResponse());
    }

    public async Task<Result<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return UserErrors.NotFound;
        }

        user.Profile ??= new UserProfile { UserId = user.Id };
        user.Profile.FirstName = NullIfBlank(request.FirstName);
        user.Profile.LastName = NullIfBlank(request.LastName);
        user.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(user.ToResponse());
    }

    public async Task<Result<PagedResult<UserResponse>>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await users.GetPagedWithProfileAsync(page, pageSize, cancellationToken);
        var items = result.Items.Select(u => u.ToResponse()).ToList();

        return Result.Success(new PagedResult<UserResponse>(items, result.Page, result.PageSize, result.TotalCount));
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
