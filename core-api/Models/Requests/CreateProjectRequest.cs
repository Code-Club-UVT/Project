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

    [Required, StringLength(500)]
    public string BaseUrl { get; init; } = string.Empty;

    [StringLength(500)]
    public string? HealthUrl { get; init; }
}
