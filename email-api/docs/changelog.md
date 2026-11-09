# Changelog

## Tests
- New `EmailApi.Tests` (37 tests): the templates (names, link checks, HTML encoding of every value), `EmailService` (unknown template, missing data, idempotency by message id, failures reach the caller and are not recorded), the internal controller, the startup rules (no sender outside Development, SMTP password checks) and the development sender

## Step 1: email-api (service split)
- Created from core-api's email code (`EmailTemplates`, `SmtpEmailSender`, `LoggingEmailSender`, `SmtpOptions`), now a separate internal service
- `POST /internal/emails` with named templates (`email-confirmation`, `password-reset`), required-key and link checks, idempotency by `messageId` (`SentEmails` in schema `email`, fresh `InitialCreate` migration)
- Internal key auth from platform-kit (`InternalAuth:Callers:identity`); `/health` with a PostgreSQL check
- Dockerfile with `api` and `migrate` targets, `CLAUDE.md`, this `docs/` folder
- `SendEmailRequest` uses init properties: MVC throws on validation attributes on a positional record's properties (every request was a 500; found by the first dev run)
- Verified on the dev server (see implementation.md)
