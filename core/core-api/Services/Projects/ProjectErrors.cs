using PlatformKit.Results;

namespace CoreApi.Services.Projects;

public static class ProjectErrors
{
    public static readonly Error NotFound = Error.NotFound("Project.NotFound", "The project was not found.");
    public static readonly Error SlugTaken = Error.Conflict("Project.SlugTaken", "A project with this slug already exists.");
    public static readonly Error SlugReserved = Error.Validation("Project.SlugReserved", "This slug is reserved.");
    public static readonly Error SystemProtected = Error.Forbidden("Project.SystemProtected", "This is a system project: its URLs and active state cannot be changed and it cannot be deleted.");
    public static readonly Error NoMemberships = Error.Validation("Project.NoMemberships", "This project forwards the original token, so it has no roles or members.");
    public static readonly Error InvalidDefaultRole = Error.Validation("Project.InvalidDefaultRole", "The default role must be one of this project's roles.");
    public static readonly Error NotMember = Error.Forbidden("Project.NotMember", "The user has no access to this project.");

    public static Error InvalidUrl(string reason) => Error.Validation("Project.InvalidUrl", reason);
}
