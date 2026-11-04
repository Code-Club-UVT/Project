using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

/// <summary>Used to create and to update a project role.</summary>
public sealed record ProjectRoleRequest
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }
}
