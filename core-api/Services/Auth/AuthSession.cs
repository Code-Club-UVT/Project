using CoreApi.Models.Responses;

namespace CoreApi.Services.Auth;

/// <summary>A new session: the response for the client plus the raw refresh token the controller puts in the cookie.</summary>
public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt);
