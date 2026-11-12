namespace PlatformKit.Internal;

/// <summary>Names used by the service-to-service (<c>/internal/...</c>) authentication.</summary>
public static class InternalAuth
{
    /// <summary>Use as <c>[Authorize(AuthenticationSchemes = InternalAuth.SchemeName)]</c> on internal controllers.</summary>
    public const string SchemeName = "InternalKey";

    public const string HeaderName = "X-Internal-Key";
}
