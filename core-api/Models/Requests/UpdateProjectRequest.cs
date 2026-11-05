using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

/// <summary>Full replacement of the editable fields. The slug and the token mode cannot change.</summary>
public sealed record UpdateProjectRequest
{
    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    [Required, StringLength(500)]
    public string BaseUrl { get; init; } = string.Empty;

    [StringLength(500)]
    public string? HealthUrl { get; init; }

    public bool IsActive { get; init; } = true;

    /// <summary>A role of this project that new users get once their email is confirmed; null (or omitted) means invite-only.</summary>
    public Guid? DefaultRoleId { get; init; }
}
