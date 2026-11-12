using PlatformKit.Results;

namespace Identity.Services.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Invalid credentials.");
    public static readonly Error EmailNotConfirmed = Error.Forbidden("Auth.EmailNotConfirmed", "The email address has not been confirmed.");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or has expired.");
    public static readonly Error InvalidToken = Error.Validation("Auth.InvalidToken", "The token is invalid or has expired.");
    public static readonly Error InvalidCurrentPassword = Error.Validation("Auth.InvalidCurrentPassword", "The current password is incorrect.");
    public static readonly Error UsernameTaken = Error.Conflict("Auth.UsernameTaken", "This username is already taken.");
    public static readonly Error UserNotFound = Error.NotFound("User.NotFound", "The user was not found.");
    public static readonly Error TooManyAttempts = Error.TooManyRequests("Auth.TooManyAttempts", "Too many failed attempts. Try again later.");
    public static readonly Error EmailLimitReached = Error.TooManyRequests("Auth.EmailLimitReached", "No more emails can be sent right now. Try again later.");
    public static readonly Error EmailTaken = Error.Conflict("Auth.EmailTaken", "This email is already registered.");
}
