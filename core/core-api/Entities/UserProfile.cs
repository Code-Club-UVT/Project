namespace CoreApi.Entities;

/// <summary>
/// A user as the core API knows it. The account itself (credentials, confirmation, active flag) belongs to identity,
/// with the same id; identity creates this row through <c>POST /internal/users</c> when the account is registered.
/// </summary>
public class UserProfile
{
    public Guid UserId { get; set; }

    /// <summary>Read copy of identity's username, kept in step by identity. Used for admin lists and member lists.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Read copy of identity's email, kept in step by identity.</summary>
    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
