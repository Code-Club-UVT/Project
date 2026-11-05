using System.Net;

namespace EmailApi.Templates;

public sealed record EmailContent(string Subject, string Html, string Text);

/// <summary>
/// The shared look of every email. The HTML is deliberately old-school so it renders in every client (Outlook
/// included): nested tables, inline styles, legacy attributes, a table-based button, a web-safe font stack and no
/// images or external CSS. Every value is encoded before it goes into the markup.
/// </summary>
internal static class EmailLayout
{
    private const string Accent = "#2563EB";
    private const string FontStack = "Arial, Helvetica, sans-serif";

    /// <summary>A heading, one paragraph, a call-to-action button with its link written out below, and a footer note.</summary>
    public static EmailContent ButtonEmail(
        string appName, string subject, string heading, string paragraph, string buttonText, string link, string footerNote)
    {
        var name = Encode(appName);
        var url = Encode(link);

        var html = $"""
            <!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
            <html xmlns="http://www.w3.org/1999/xhtml">
            <head>
            <meta http-equiv="Content-Type" content="text/html; charset=UTF-8" />
            <title>{Encode(subject)}</title>
            </head>
            <body style="margin:0; padding:0; background-color:#F3F4F6;" bgcolor="#F3F4F6">
            <table width="100%" border="0" cellpadding="0" cellspacing="0" bgcolor="#F3F4F6" style="background-color:#F3F4F6;">
              <tr>
                <td align="center" style="padding:24px 12px;">
                  <table width="600" border="0" cellpadding="0" cellspacing="0" style="width:600px; max-width:100%;">
                    <tr>
                      <td bgcolor="{Accent}" style="background-color:{Accent}; padding:20px 32px; font-family:{FontStack}; font-size:20px; font-weight:bold; color:#FFFFFF;">{name}</td>
                    </tr>
                    <tr>
                      <td bgcolor="#FFFFFF" style="background-color:#FFFFFF; padding:32px; font-family:{FontStack}; font-size:15px; line-height:22px; color:#374151;">
                        <h1 style="margin:0 0 16px 0; font-family:{FontStack}; font-size:22px; line-height:28px; color:#111827;">{Encode(heading)}</h1>
                        <p style="margin:0 0 24px 0;">{Encode(paragraph)}</p>
                        <table border="0" cellpadding="0" cellspacing="0">
                          <tr>
                            <td align="center" bgcolor="{Accent}" style="background-color:{Accent}; border-radius:4px;">
                              <a href="{url}" style="display:inline-block; padding:12px 28px; font-family:{FontStack}; font-size:15px; font-weight:bold; color:#FFFFFF; text-decoration:none;">{Encode(buttonText)}</a>
                            </td>
                          </tr>
                        </table>
                        <p style="margin:24px 0 4px 0; font-size:13px; color:#6B7280;">Button not working? Copy this link into your browser:</p>
                        <p style="margin:0; font-size:13px; line-height:18px; word-break:break-all;"><a href="{url}" style="color:{Accent};">{url}</a></p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:20px 32px; font-family:{FontStack}; font-size:12px; line-height:18px; color:#6B7280;">{Encode(footerNote)}</td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            </body>
            </html>
            """;

        var text = $"""
            {heading}

            {paragraph}

            {link}

            {footerNote}
            """;

        return new EmailContent(subject, html, text);
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
