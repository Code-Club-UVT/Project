using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models.Requests;

public sealed record ChangeProjectRoleRequest
{
    [Required]
    public Guid RoleId { get; init; }
}
