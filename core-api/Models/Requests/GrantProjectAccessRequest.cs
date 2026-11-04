using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

public sealed record GrantProjectAccessRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public Guid RoleId { get; init; }
}
