using CoreApi.Common.Results;

namespace CoreApi.Services.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Invalid credentials.");
    public static readonly Error EmailNotConfirmed = Error.Forbidden("Auth.EmailNotConfirmed", "The email address has not been confirmed.");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or has expired.");
    public static readonly Error InvalidToken = Error.Validation("Auth.InvalidToken", "The token is invalid or has expired.");
    public static readonly Error InvalidCurrentPassword = Error.Validation("Auth.InvalidCurrentPassword", "The current password is incorrect.");
    public static readonly Error UsernameTaken = Error.Conflict("Auth.UsernameTaken", "This username is already taken.");
    public static readonly Error EmailTaken = Error.Conflict("Auth.EmailTaken", "This email is already registered.");
}
