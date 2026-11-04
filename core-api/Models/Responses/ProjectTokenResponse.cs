namespace CoreApi.Models.Responses;

public sealed record ProjectTokenResponse(string Token, DateTime ExpiresAt, string RoleName);
