namespace Identity.Models.Responses;

/// <summary>The account as identity knows it. Profile fields and project access come from the core API (<c>/api/core/users/me</c>).</summary>
public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    bool EmailConfirmed,
    DateTime CreatedAt);
