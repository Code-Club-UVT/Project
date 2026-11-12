namespace EmailApi.Templates;

/// <summary>Base for the emails that are one message and one button with a link (the <c>link</c> data key).</summary>
public abstract class LinkEmailTemplate : IEmailTemplate
{
    public const string LinkKey = "link";

    public abstract string Name { get; }

    public IReadOnlyCollection<string> RequiredKeys { get; } = [LinkKey];

    protected abstract string Subject { get; }
    protected abstract string Paragraph { get; }
    protected abstract string ButtonText { get; }
    protected abstract string FooterNote { get; }

    public string? Validate(IReadOnlyDictionary<string, string> data)
    {
        return Uri.TryCreate(data[LinkKey], UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? null
            : "The link must be an absolute http or https URL.";
    }

    public EmailContent Render(string appName, IReadOnlyDictionary<string, string> data)
    {
        return EmailLayout.ButtonEmail(appName, Subject, Subject, Paragraph, ButtonText, data[LinkKey], FooterNote);
    }
}

public sealed class EmailConfirmationTemplate : LinkEmailTemplate
{
    public override string Name => "email-confirmation";
    protected override string Subject => "Confirm your email address";
    protected override string Paragraph => "Welcome! Please confirm your email address to finish creating your account.";
    protected override string ButtonText => "Confirm email";
    protected override string FooterNote => "If you did not create an account, you can ignore this message.";
}

public sealed class PasswordResetTemplate : LinkEmailTemplate
{
    public override string Name => "password-reset";
    protected override string Subject => "Reset your password";
    protected override string Paragraph => "We received a request to reset your password. Use the button below to choose a new one.";
    protected override string ButtonText => "Choose a new password";
    protected override string FooterNote => "If you did not request this, you can ignore this message. Your password will not change.";
}
