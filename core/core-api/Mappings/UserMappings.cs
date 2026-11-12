using CoreApi.Entities;
using CoreApi.Models.Responses;

namespace CoreApi.Mappings;

public static class UserMappings
{
    public static UserResponse ToResponse(this UserProfile profile)
    {
        return new UserResponse(profile.UserId, profile.Username, profile.Email, profile.FirstName, profile.LastName, profile.CreatedAt);
    }
}
