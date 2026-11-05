using PlatformKit.Results;
using CoreApi.Models.Requests;
using CoreApi.Models.Responses;

namespace CoreApi.Services.Users;

public interface IUserService
{
    Task<Result<UserResponse>> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<UserResponse>>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
