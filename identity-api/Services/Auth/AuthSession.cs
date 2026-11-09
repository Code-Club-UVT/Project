using Identity.Models.Responses;

namespace Identity.Services.Auth;

/// <summary>
/// A session for the client: the response plus the raw refresh token the controller puts in the cookie. Both refresh
/// fields are null when only a new access token is given (a refresh that lost a race with another one, see
/// <c>RefreshTokenOptions.ReuseGraceSeconds</c>): the cookie from the other response stays.
/// </summary>
public sealed record AuthSession(AuthResponse Response, string? RefreshToken, DateTime? RefreshTokenExpiresAt);
