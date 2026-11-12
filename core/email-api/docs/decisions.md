# Decisions

| Decision | Reason |
|---|---|
| Email sending behind an interface, with a simple dev implementation that logs the links | No SMTP server needed yet |
| MailKit for SMTP, selected by `Smtp:Enabled` | `System.Net.Mail.SmtpClient` is marked obsolete; MailKit is maintained and supports STARTTLS properly. The same code works in Docker, configured through `Smtp__*` environment variables |
| Email HTML as nested tables with inline styles (no modern CSS) | Renders in all clients, Outlook included; flexbox, grid and external CSS are unreliable in email |
| A separate email service; callers send `{ messageId, template, to, data }`, never HTML | Branding stays in one place and callers cannot send arbitrary content; templates are classes with required keys, validated before sending |
| Internal only: no gateway route, only `InternalAuth` callers | Nothing outside the platform should be able to send mail through it |
| Sent inline; retries come from the caller's outbox | The caller already has a durable queue; a second one here would add nothing for the current volume |
| `SentEmails` (message id, template, recipient, caller, time) for idempotency; the body is never stored | A retried delivery is not sent twice; a stored body would keep raw tokens |
| At-least-once: the id is recorded after the mail server accepted the email | Recording first would lose an email when sending fails; a crash between send and record means at most one duplicate |
| Development uses the logging sender unless `Smtp:Enabled`; elsewhere startup fails without SMTP | No silent loss of mail in a deployed environment |
| Own schema `email`, `IDesignTimeDbContextFactory` for migrations | Same reasons as identity |
