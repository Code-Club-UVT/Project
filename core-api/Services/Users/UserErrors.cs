using CoreApi.Common.Results;

namespace CoreApi.Services.Users;

public static class UserErrors
{
    public static readonly Error NotFound = Error.NotFound("User.NotFound", "The user was not found.");
}
