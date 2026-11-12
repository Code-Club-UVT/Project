using Identity.Entities;
using Identity.Models.Responses;

namespace Identity.Mappings;

public static class UserMappings
{
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse(user.Id, user.Username, user.Email, user.EmailConfirmed, user.CreatedAt);
    }
}
