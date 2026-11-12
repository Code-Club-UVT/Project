using PlatformKit.Results;

namespace CoreApi.Services.ProjectMembers;

public static class ProjectMemberErrors
{
    public static readonly Error NotFound = Error.NotFound("ProjectMember.NotFound", "The user has no access to this project.");
    public static readonly Error AlreadyMember = Error.Conflict("ProjectMember.AlreadyMember", "The user already has access to this project. Change their role instead.");
}
