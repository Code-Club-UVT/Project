namespace CoreApi.Services.UserTabs;

public static class UserTabRules
{
    public const int MaxTabs = 20;

    /// <summary>
    /// An app-internal path: one leading "/" (so never a scheme, a host or a protocol-relative "//"), no backslash and
    /// no control characters. The harness opens it below the app's own address, never as a URL of its own.
    /// </summary>
    public static bool IsValidPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/')
        {
            return false;
        }

        if (path.Length > 1 && (path[1] == '/' || path[1] == '\\'))
        {
            return false;
        }

        return !path.Any(c => c == '\\' || char.IsControl(c));
    }
}
