using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

public sealed record UpdateProfileRequest
{
    [StringLength(100)]
    public string? FirstName { get; init; }

    [StringLength(100)]
    public string? LastName { get; init; }
}
