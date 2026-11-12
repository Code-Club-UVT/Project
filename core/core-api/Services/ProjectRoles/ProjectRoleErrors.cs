using PlatformKit.Results;

namespace CoreApi.Services.ProjectRoles;

public static class ProjectRoleErrors
{
    public static readonly Error NotFound = Error.NotFound("ProjectRole.NotFound", "The role was not found in this project.");
    public static readonly Error NameTaken = Error.Conflict("ProjectRole.NameTaken", "This project already has a role with this name.");
    public static readonly Error SystemRole = Error.Forbidden("ProjectRole.SystemRole", "The Admin role of the core project grants platform admin rights; it cannot be renamed or deleted.");
    public static readonly Error InUse = Error.Conflict("ProjectRole.InUse", "The role is assigned to users. Move them to another role first.");
}
