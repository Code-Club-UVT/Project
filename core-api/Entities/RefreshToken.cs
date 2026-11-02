namespace CoreApi.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Shared by every token rotated from the same login.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>SHA-256 (hex) of the raw token. The raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>Time of the original login, copied to each rotated token (absolute lifetime cap).</summary>
    public DateTime FamilyStartedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public User User { get; set; } = null!;
}
