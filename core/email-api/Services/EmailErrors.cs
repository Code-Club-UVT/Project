using PlatformKit.Results;

namespace EmailApi.Services;

public static class EmailErrors
{
    public static Error UnknownTemplate(string name) =>
        Error.Validation("Email.UnknownTemplate", $"There is no email template named '{name}'.");

    public static Error MissingData(string template, IEnumerable<string> keys) =>
        Error.Validation("Email.MissingData", $"Template '{template}' needs these data keys: {string.Join(", ", keys)}.");

    public static Error InvalidData(string message) =>
        Error.Validation("Email.InvalidData", message);
}
