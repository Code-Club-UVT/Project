using Identity.Models.Responses;

namespace Identity.Services.Auth;

/// <summary>A new session: the response for the client plus the raw refresh token the controller puts in the cookie.</summary>
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt);
