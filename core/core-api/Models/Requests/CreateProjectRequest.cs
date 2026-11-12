using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

public sealed record CreateProjectRequest
{
    [Required, StringLength(50, MinimumLength = 2), RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "The slug may only contain lowercase letters, digits and single hyphens.")]
    public string Slug { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    /// <summary>Where the gateway forwards API calls. Omit for a project without a backend.</summary>
    [StringLength(500)]
    public string? BackendUrl { get; init; }

    /// <summary>Internal address of the static frontend. Omit for a project without a frontend.</summary>
    [StringLength(500)]
    public string? FrontendUrl { get; init; }

    [StringLength(500)]
    public string? HealthUrl { get; init; }
}
