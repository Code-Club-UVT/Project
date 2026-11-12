namespace EmailApi.Templates;

/// <summary>
/// One kind of email. Callers pick it by <see cref="Name"/> and send only the data it needs, never markup, so the
/// look stays in one place and a caller cannot send arbitrary content. Add a new template as a class implementing
/// this interface and register it in <c>AddEmailServices</c>.
/// </summary>
public interface IEmailTemplate
{
    /// <summary>Lowercase, hyphenated, for example <c>email-confirmation</c>. Part of the internal API, so it never changes.</summary>
    string Name { get; }

    /// <summary>The data keys the template needs; a request missing one of them is rejected.</summary>
    IReadOnlyCollection<string> RequiredKeys { get; }

    /// <summary>Checks the values beyond their presence (for example that a link is an absolute http(s) URL). Null when valid.</summary>
    string? Validate(IReadOnlyDictionary<string, string> data);

    EmailContent Render(string appName, IReadOnlyDictionary<string, string> data);
}
