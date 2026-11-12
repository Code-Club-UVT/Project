using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

/// <summary>Sent by identity when an account is registered (its outbox; can arrive more than once).</summary>
public sealed record InternalUserRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required, StringLength(50)]
    public string Username { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? LastName { get; init; }
}
