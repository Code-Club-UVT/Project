using Identity.Enums;

namespace Identity.Entities;

/// <summary>Single-use token for email confirmation and password reset.</summary>
public class UserToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserTokenType Type { get; set; }

    /// <summary>SHA-256 (hex) of the raw token. The raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public User User { get; set; } = null!;
}
