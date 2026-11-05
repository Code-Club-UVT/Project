# Changelog

## Step 1: email-api (service split)
- Created from core-api's email code (`EmailTemplates`, `SmtpEmailSender`, `LoggingEmailSender`, `SmtpOptions`), now a separate internal service
- `POST /internal/emails` with named templates (`email-confirmation`, `password-reset`), required-key and link checks, idempotency by `messageId` (`SentEmails` in schema `email`, fresh `InitialCreate` migration)
- Internal key auth from platform-kit (`InternalAuth:Callers:identity`); `/health` with a PostgreSQL check
- Dockerfile with `api` and `migrate` targets, `CLAUDE.md`, this `docs/` folder
