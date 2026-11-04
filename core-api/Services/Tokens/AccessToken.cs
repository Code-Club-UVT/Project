namespace CoreApi.Services.Tokens;

public sealed record AccessToken(string Token, DateTime ExpiresAt);
