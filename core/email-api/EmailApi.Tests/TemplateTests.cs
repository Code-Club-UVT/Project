using EmailApi.Templates;
using Xunit;

namespace EmailApi.Tests;

public class TemplateTests
{
    private static readonly IReadOnlyDictionary<string, string> Link = new Dictionary<string, string> { ["link"] = "https://example.org/dev/confirm-email?token=abc" };

    public static TheoryData<IEmailTemplate> Templates => new() { new EmailConfirmationTemplate(), new PasswordResetTemplate() };

    // The names are part of the internal API that identity calls: they must never change.
    [Fact]
    public void The_template_names_are_the_ones_identity_asks_for()
    {
        Assert.Equal("email-confirmation", new EmailConfirmationTemplate().Name);
        Assert.Equal("password-reset", new PasswordResetTemplate().Name);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void A_link_is_the_only_data_needed(IEmailTemplate template)
    {
        Assert.Equal(["link"], template.RequiredKeys);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Http_and_https_links_are_valid(IEmailTemplate template)
    {
        Assert.Null(template.Validate(new Dictionary<string, string> { ["link"] = "https://example.org/x?token=1&a=b" }));
        Assert.Null(template.Validate(new Dictionary<string, string> { ["link"] = "http://localhost:5173/reset-password?token=1" }));
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Anything_but_an_absolute_http_link_is_refused(IEmailTemplate template)
    {
        foreach (var bad in new[] { "/relative/path", "example.org/x", "ftp://example.org/x", "javascript:alert(1)", "mailto:a@b.c", "data:text/html,<b>x</b>", "file:///etc/passwd", "not a url" })
        {
            Assert.NotNull(template.Validate(new Dictionary<string, string> { ["link"] = bad }));
        }
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void The_email_has_the_app_name_the_link_in_both_bodies_and_a_subject(IEmailTemplate template)
    {
        var content = template.Render("Code Club", Link);

        Assert.False(string.IsNullOrWhiteSpace(content.Subject));
        Assert.Contains("Code Club", content.Html);
        Assert.Contains("href=\"https://example.org/dev/confirm-email?token=abc\"", content.Html);
        Assert.Contains("https://example.org/dev/confirm-email?token=abc", content.Text);
        Assert.Contains(content.Subject, content.Text);
        Assert.DoesNotContain("<", content.Text);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Values_are_encoded_so_they_cannot_add_markup(IEmailTemplate template)
    {
        var data = new Dictionary<string, string> { ["link"] = "https://example.org/x?a=1&b=\"><script>alert(1)</script>" };

        var content = template.Render("<b>Club</b> & Co", data);

        Assert.DoesNotContain("<script>", content.Html);
        Assert.DoesNotContain("<b>Club</b>", content.Html);
        Assert.Contains("&amp;", content.Html);
        Assert.Contains("&lt;b&gt;Club&lt;/b&gt;", content.Html);
    }

    [Fact]
    public void The_two_emails_say_different_things()
    {
        var confirmation = new EmailConfirmationTemplate().Render("App", Link);
        var reset = new PasswordResetTemplate().Render("App", Link);

        Assert.Equal("Confirm your email address", confirmation.Subject);
        Assert.Equal("Reset your password", reset.Subject);
        Assert.Contains("did not create an account", confirmation.Text);
        Assert.Contains("password will not change", reset.Text);
    }
}
