namespace PlatformKit.Internal;

/// <summary>
/// The services allowed to call this API's <c>/internal/...</c> endpoints, each with its own key, for example
/// <c>"InternalAuth": { "Callers": { "gateway": "..." } }</c>. The caller's name becomes the <c>sub</c> claim.
/// </summary>
public sealed class InternalAuthOptions
{
    public const string SectionName = "InternalAuth";
    public const string PlaceholderKeyPrefix = "CHANGE-ME";
    public const int MinKeyLength = 32;

    public Dictionary<string, string> Callers { get; set; } = new(StringComparer.Ordinal);
}
