using CoreApi.Entities;
using CoreApi.Models.Responses;

namespace CoreApi.Mappings;

public static class UserMappings
{
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            user.EmailConfirmed,
            user.Profile?.FirstName,
            user.Profile?.LastName,
            user.CreatedAt);
    }
}
