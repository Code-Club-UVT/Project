using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

/// <summary>Sent by the gateway to get a token for a user in a project.</summary>
public sealed record ProjectTokenRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required, StringLength(50)]
    public string Slug { get; init; } = string.Empty;
}
