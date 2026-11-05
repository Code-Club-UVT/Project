using PlatformKit.Results;

namespace Identity.Services.Tokens;

public static class TokenErrors
{
    public static readonly Error UserNotFound = Error.NotFound("User.NotFound", "The user was not found.");
    public static readonly Error ProjectNotFound = Error.NotFound("Project.NotFound", "The project was not found.");
    public static readonly Error NotMember = Error.Forbidden("Project.NotMember", "The user has no access to this project.");
}
