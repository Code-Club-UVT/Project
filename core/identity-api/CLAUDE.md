# CLAUDE.md

Instructions for `core/identity-api/`. The root [CLAUDE.md](../../CLAUDE.md) also applies.

## Overview

The identity service (ASP.NET Core Web API, .NET 10, controllers, project `IdentityApi`, namespace `Identity`). It owns accounts and every token:

- credentials and the account flows: register, confirm email, resend confirmation, login, refresh, logout, forgot/reset/change password;
- the one signing key: edge tokens, refresh tokens, project tokens (minted for the gateway), the JWKS;
- an outbox that tells core-api about new and confirmed accounts and asks email-api to send emails.

It does not hold profiles, projects or roles (core-api) and does not render or send email (email-api). Behind the gateway it is the system project `identity` (PassThrough: login and register are anonymous, so the edge token is forwarded unchanged and identity validates it itself).

Read [docs/auth-design.md](docs/auth-design.md) for the token design and flows.

## Commands

Run from `core/identity-api/`:

- Build: `dotnet build`
- Test: `dotnet test Identity.Tests` (in-memory fakes of the repositories: no Docker or database needed)
- Run: `dotnet run` (http://localhost:5060, Development)
- Add a migration: `dotnet ef migrations add <Name> --context PostgresDbContext --output-dir Data/Postgres/Migrations`
- Apply migrations: `dotnet ef database update --context PostgresDbContext`
- Docker image (repo root as context, because of platform-kit): `docker build -f core/identity-api/Dockerfile --target api -t identity-api .` (`--target migrate` for the migration bundle)

## Rules

- Same N-layer rules as core-api (see [../core-api/docs/architecture.md](../core-api/docs/architecture.md)): thin controllers returning `ToActionResult(result)`, business logic in services that return `Result`, repositories behind interfaces that never call `SaveChanges` (services commit through `IUnitOfWork`), DTOs in `Models/Requests` and `Models/Responses`, async I/O with a `CancellationToken`.
- Own tables in schema `identity` only (`PostgresDbContext`, migrations history in that schema too). Never edit an applied migration.
- `CoreReadDbContext` maps three tables of core-api's schema (`core.Projects`, `core.ProjectRoles`, `core.ProjectMembers`) read-only, with only the columns needed for project tokens. Never write through it and never migrate it.
- Anything another service must learn about goes through `IOutbox` in the same `SaveChangesAsync` as the change; never call core-api or email-api inline from a request. Receivers must stay idempotent.
- Never store raw passwords or tokens; only hashes. Never log them. Outbox email bodies hold a link with a raw token: never log message bodies.
- Endpoints require authentication by default; anonymous ones must say `[AllowAnonymous]`. Internal endpoints live under `/internal/...` with `[Authorize(AuthenticationSchemes = InternalAuth.SchemeName)]`.
- The claim names in `TokenService` (`sub`, `username`, `role`, `email`) are what platform-kit and the gateway read; change them together.
- Ask before adding NuGet dependencies.
- Configuration lives in `appsettings*.json`, overridable with environment variables (`Section__Key`). Committed secrets are clearly marked placeholders (`CHANGE-ME...`); real values come from the environment (on the server: `<env>/config/identity-api.json`). Local real values go in the git-ignored `appsettings.Development.local.json`.

## Tests

`Identity.Tests/` is its own project (xunit, fakes written by hand, no mocking library) inside this folder; the API project excludes it and the Docker context leaves it out. A change to behaviour comes with a test. `.github/workflows/ci.yml` runs it on every pull request and before every deployment, so a failing test blocks a deploy. Tests that need a real database are not built yet (see the backlog).

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)). Read it at the start of a session.
