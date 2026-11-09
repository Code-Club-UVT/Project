# CLAUDE.md

Instructions for `platform-kit/`. The root [CLAUDE.md](../CLAUDE.md) also applies.

## Overview

A shared class library (`PlatformKit`, .NET 10) that every service references (identity-api, core-api, email-api, gateway, mock-api). It holds the cross-cutting pieces each service would otherwise copy:

- **Auth**: validation of the tokens identity issues (ES256, keys from identity's JWKS), with a "must be logged in" fallback policy.
- **Request context**: `IRequestContext` (user, role, project, correlation id, client IP), the correlation id middleware and its propagation on outgoing calls, `/health`.
- **Results**: `Result`, `Result<T>`, `Error`, `ErrorType`, `PagedResult<T>`.
- **Web**: `ResultControllerBase` (Result to ProblemDetails with a `code`) and the global exception handler.
- **Internal**: the `InternalKey` scheme for service-to-service `/internal/...` endpoints and an HttpClient helper that sends the key.

It is not a web app and has no business logic.

## Commands

Run from `platform-kit/`:

- Build: `dotnet build`
- Test: `dotnet test PlatformKit.Tests` (no Docker or database needed). Add or change a test with every change to the shared behaviour; a breaking change here breaks every service.

Services reference it by project reference (`<ProjectReference Include="..\platform-kit\PlatformKit.csproj" />`). Because of that, a service's Dockerfile must use the repo root as build context.

## Rules

- Keep it generic: nothing that belongs to one service.
- Validation must stay strict and fail closed: pinned algorithm (ES256), issuer, audience (the service's slug), lifetime, signature from the published keys. Do not add switches that weaken it; loosening it affects every API. The same goes for the internal key scheme (constant-time compare, placeholder keys refused outside Development).
- The public surface is the extension methods (`PlatformAuthExtensions`, `PlatformWebExtensions`, `InternalAuthExtensions`), `IRequestContext`, `ResultControllerBase`, the `Results` types and `InternalAuth`; everything else is `internal`. Changing any of them is a change for every service, so note it in `docs/changelog.md`.
- Ask before adding NuGet dependencies.
- Settings come from the `PlatformAuth` and `InternalAuth` configuration sections and can be overridden by environment variables (`PlatformAuth__Audience`, `InternalAuth__Callers__gateway`, ...).
- All claim names (`sub`, `username`, `role`) must match what identity-api's `TokenService` writes; change both together.

## Tests

`PlatformKit.Tests/` is its own project (xunit, no mocking library); `PlatformKit.csproj` excludes it and lets it see the internal types (`InternalsVisibleTo`). `.github/workflows/ci.yml` runs it on every pull request and before every deployment.

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)). Read it at the start of a session.
