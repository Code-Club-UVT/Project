using PlatformKit.Results;

namespace CoreApi.Services.UserTabs;

public static class UserTabErrors
{
    public static readonly Error TooMany = Error.Validation("UserTab.TooMany", $"At most {UserTabRules.MaxTabs} tabs can be saved.");
    public static readonly Error InvalidPath = Error.Validation("UserTab.InvalidPath", "A tab path must start with a single '/' and contain no backslash or control characters.");
}
