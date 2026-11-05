# Current implementation

## Stack

.NET 10 Web API with controllers, project `EmailApi`; EF Core 10 with PostgreSQL; MailKit for SMTP; platform-kit for results, ProblemDetails, request context, health and the internal key scheme.

## Layout

```
email-api/
  Controllers/InternalEmailsController.cs   POST /internal/emails
  Services/                 EmailService (validate, skip duplicates, render, send, record), EmailErrors
  Templates/                IEmailTemplate, LinkEmailTemplate (+ EmailConfirmationTemplate, PasswordResetTemplate), EmailLayout
  Senders/                  IEmailSender, SmtpEmailSender (MailKit), LoggingEmailSender (Development)
  Data/Postgres/            PostgresDbContext (schema email), SentEmailRepository, Configurations/, Migrations/, DesignTimeDbContextFactory
  Entities/SentEmail.cs
  Models/SendEmailRequest.cs
  Options/SmtpOptions.cs
  Extensions/               DI (Postgres + health, internal-only auth, email services)
  Dockerfile                targets api and migrate (EF bundle)
```

## Endpoint

`POST /internal/emails` (internal key of a caller in `InternalAuth:Callers`):

```json
{ "messageId": "6f1c...", "template": "email-confirmation", "to": "user@example.com", "data": { "link": "https://..." } }
```

| Answer | When |
|---|---|
| 204 | sent, or this `messageId` was sent before (nothing is sent again) |
| 400 `Email.UnknownTemplate`, `Email.MissingData`, `Email.InvalidData` | unknown template, a required key missing or blank, a value that fails the template's check (a link must be absolute http/https) |
| 401 | missing or wrong internal key |
| 500 | delivery failed; the caller retries |

`/health` is anonymous (PostgreSQL check).

## Templates

| Name | Data | Subject |
|---|---|---|
| `email-confirmation` | `link` | Confirm your email address |
| `password-reset` | `link` | Reset your password |

Both use `EmailLayout.ButtonEmail`: a header with the app name (`Smtp:FromName`), a heading, one paragraph, a button, the link written out, and a footer note; plus a plain-text part.

## Database (schema `email`)

`SentEmails`: `MessageId` (PK, the caller's id), `Template`, `To`, `Caller`, `SentAt` (indexed). The body is never stored.

## Configuration

| Section | Keys |
|---|---|
| `ConnectionStrings:PostgresConnection` | database `platform` (role with access to schema `email`) |
| `InternalAuth:Callers` | `identity`: the key identity sends |
| `Smtp` | `Enabled`, `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`, `FromName`, `TimeoutSeconds` |

Development without `Smtp:Enabled` writes each email (recipient, subject, text body with the link) to the log; that is how tests read confirmation and reset links. `Testing:RecipientEmail` in `appsettings.Development.json` is an address for manual SMTP tests; no code reads it.

## Verified

On the dev server (2026-11-05, commit 3252626, through Caddy and the gateway at `https://codeclub.info.uvt.ro/dev`), a scripted run of 83 checks: 79 passed; the other 4 were test mistakes, not bugs (the outbox still retrying rows from a broken earlier deploy, a wrong field name, the gateway's 30-second access cache, and the per-IP auth rate limit kicking in). A re-check confirmed a grant takes effect after the cache time. One real email was sent over SMTP. Covered here: confirmation and reset emails rendered and logged (Development), `SentEmails` filled with identity's message id and caller, one email per message after outbox retries, and one real email over SMTP to the test address.
