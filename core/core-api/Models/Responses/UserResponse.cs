namespace CoreApi.Models.Responses;

/// <summary>A user's profile. Account data (password, confirmation) belongs to identity.</summary>
public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string? FirstName,
    string? LastName,
    DateTime CreatedAt);
