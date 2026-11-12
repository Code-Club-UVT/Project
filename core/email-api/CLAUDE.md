# CLAUDE.md

Instructions for `core/email-api/`. The root [CLAUDE.md](../../CLAUDE.md) also applies.

## Overview

The email service (ASP.NET Core Web API, .NET 10, controllers, project `EmailApi`). Other services ask it to send an email by template name: `POST /internal/emails { messageId, template, to, data }`. It renders the template (the look and branding live only here) and sends it over SMTP. It is internal only: no gateway route, only callers listed in `InternalAuth:Callers` (today identity).

## Commands

Run from `core/email-api/`:

- Build: `dotnet build`
- Test: `dotnet test EmailApi.Tests` (no Docker or database needed)
- Run: `dotnet run` (http://localhost:5070, Development; emails are written to the log unless `Smtp:Enabled`)
- Add a migration: `dotnet ef migrations add <Name> --output-dir Data/Postgres/Migrations`
- Apply migrations: `dotnet ef database update`
- Docker image (repo root as context, because of platform-kit): `docker build -f core/email-api/Dockerfile --target api -t email-api .` (`--target migrate` for the migration bundle)

## Rules

- Callers send data, never markup. A new email is a new `IEmailTemplate` class in `Templates/` (name, required keys, validation, rendering through `EmailLayout`), registered in `AddEmailServices`. Template names are part of the internal API: never rename one.
- Email HTML stays old-school (nested tables, inline styles, no external CSS or images) so it renders in every client, Outlook included. Encode every value.
- Never store or log email bodies: they hold links with raw tokens. The Development-only `LoggingEmailSender` is the one exception. `SentEmails` keeps only id, template, recipient, caller and time.
- Keep it idempotent by `messageId`; retries are the caller's job (identity's outbox).
- Own tables in schema `email` only. Never edit an applied migration.
- Every endpoint requires the internal key (fallback policy on the `InternalKey` scheme).
- Ask before adding NuGet dependencies.
- Configuration lives in `appsettings*.json`, overridable with environment variables (`Smtp__Password`, ...). Committed secrets are clearly marked placeholders (`CHANGE-ME...`). The local SMTP password goes in the git-ignored `appsettings.Development.local.json`. Outside Development startup fails unless `Smtp:Enabled` is true with a real password.

## Tests

`EmailApi.Tests/` is its own project (xunit, fakes written by hand, no mocking library) inside this folder; the API project excludes it and the Docker context leaves it out. A change to behaviour comes with a test. `.github/workflows/ci.yml` runs it on every pull request and before every deployment, so a failing test blocks a deploy. Tests that need a real database are not built yet (see the backlog).

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)). Read it at the start of a session.
