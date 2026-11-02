namespace CoreApi.Models.Responses;

/// <summary>Returned by login and refresh. The refresh token travels only in an HttpOnly cookie.</summary>
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);
