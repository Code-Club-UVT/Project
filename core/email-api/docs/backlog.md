# Backlog and open points

- **Email delivery for production**: Gmail or a university mailbox with an app password is for testing (sending limits, may be flagged). Pick a transactional provider or a domain mailbox with SPF/DKIM.
- **Per-recipient throttling**: a caller bug or abuse could send many emails to one address. Identity should throttle resend/forgot per user (identity backlog); a cap here would be a second line.
- **Queue inside the service**: not needed while identity's outbox retries. Revisit if more callers come that have no outbox.
- **Cleanup** of old `SentEmails` rows (keep, say, 90 days).
- **Localisation**: templates are English only; a `locale` field and per-locale texts would be needed for Romanian.
- **More templates** as features need them (welcome, access granted, project invitation).
- **Preview**: a Development-only endpoint that renders a template without sending, for designing emails.
- **Tests that need a real database** (phase 2, not built): the unique key on the message id (the lost race in `SentEmailRepository.TryAddAsync`) and the migrations. `EmailApi.Tests` covers the templates, the service and the startup rules with fakes; `SmtpEmailSender` needs a mail server to be tested.
