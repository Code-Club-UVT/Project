# Current implementation

## Stack

- .NET 10, ASP.NET Core Web API with controllers; project `IdentityApi`, namespace `Identity`
- EF Core 10 with PostgreSQL; `dotnet-ef` is a local tool (`dotnet-tools.json`)
- platform-kit for results, ProblemDetails, request context, health and the internal key scheme
- `Microsoft.AspNetCore.Authentication.JwtBearer` (its own edge tokens), `Microsoft.IdentityModel.JsonWebTokens` (signing)
- OpenAPI at `/openapi/v1.json` and Swagger UI at `/swagger` in Development only

## Layout

```
identity-api/
  Controllers/        AuthController (/api/auth), AccountController (/api/account), InternalProjectTokensController,
                      WellKnownController (JWKS), ApiControllerBase
  Common/             PasswordRules, PostgresHealthCheck, BearerSecuritySchemeTransformer
  Data/
    Postgres/         PostgresDbContext (schema identity), Configurations/, Migrations/, Repositories/, DesignTimeDbContextFactory
    CoreRead/         CoreReadDbContext (read-only core.Projects, ProjectRoles, ProjectMembers), ProjectAccessReader
  Entities/           User, RefreshToken, UserToken, OutboxMessage
  Enums/              UserTokenType
  Extensions/         DI (Postgres, repositories, services, outbox, auth), ClaimsPrincipal helper
  Mappings/, Models/Requests, Models/Responses, Options/
  Repositories/Abstractions/   IRepository<,>, IUnitOfWork, users, refresh tokens, user tokens, outbox
  Services/
    Auth/             AuthService (login, refresh, logout), AccountService (register, confirm, resend, forgot, reset, change password)
    Tokens/           TokenService, SigningKeyProvider, ProjectTokenService
    Outbox/           IOutbox / Outbox (queues messages), OutboxDispatcher (delivers them)
    Seeding/          AdminSeeder
  Dockerfile          targets api and migrate (EF bundle)
  IdentityApi.http    sample requests
```

## Database (schema `identity`)

```
User 1 -- * RefreshToken
User 1 -- * UserToken
OutboxMessage (standalone)
```

| Table | Columns |
|---|---|
| `Users` | `Id`, `Username` citext(50) unique, no `@` (check), `Email` citext(256) unique, `PasswordHash`, `EmailConfirmed`, `IsActive`, `CreatedAt`, `UpdatedAt` |
| `RefreshTokens` | `Id`, `UserId`, `FamilyId`, `TokenHash` (unique), `CreatedAt`, `ExpiresAt`, `FamilyStartedAt`, `RevokedAt?`, `ReplacedByTokenId?` |
| `UserTokens` | `Id`, `UserId`, `Type` (`EmailConfirmation`, `PasswordReset`), `TokenHash` (unique), `CreatedAt`, `ExpiresAt`, `UsedAt?` |
| `OutboxMessages` | `Id`, `Target` (`core`, `email`), `Type`, `Path`, `Body` (jsonb), `CreatedAt`, `Attempts`, `NextAttemptAt`, `FailedAt?`, `LastError?`; partial index on `NextAttemptAt` where not failed |

`citext` is created by the migration (in `public`). The migrations history is `identity."__EFMigrationsHistory"`.

Read-only (owned by core-api): `core.Projects` (`Id`, `Slug`, `TokenMode`, `IsActive`), `core.ProjectRoles` (`Id`, `ProjectId`, `Name`), `core.ProjectMembers` (`UserId`, `ProjectId`, `ProjectRoleId`). The gateway reads `identity.Users` (`Id`, `IsActive`).

## Outbox

| Message | Target and path | When |
|---|---|---|
| `user-registered` | core `POST internal/users` `{ userId, username, email, firstName, lastName }` | register, admin seed |
| `user-confirmed` | core `POST internal/users/{id}/confirmed` | confirm email, admin seed |
| `email-confirmation` | email `POST internal/emails` `{ messageId, template, to, data: { link } }` | register, resend confirmation |
| `password-reset` | email `POST internal/emails` | forgot password |

- Written by `IOutbox` in the same `SaveChangesAsync` as the change.
- `OutboxDispatcher` (hosted service) wakes every `Outbox:PollSeconds`, claims up to `BatchSize` due rows (oldest first) with a lease of `LeaseSeconds`, and POSTs each with the target's internal key, the correlation id handler and `Idempotency-Key: <id>`. A 2xx deletes the row. Anything else (status, timeout, connection error) increments `Attempts`, stores `LastError` and waits `FirstBackoffSeconds * 2^(attempts-1)`, at most `MaxBackoffSeconds`; after `MaxAttempts` the row gets `FailedAt` and stays. A full batch is followed immediately by the next one.
- Order: rows are delivered oldest first, so a user's `user-registered` goes before `user-confirmed`. If core is down both fail and are retried; `confirmed` answers 404 until the profile exists.
- Inspect: `SELECT "Type", "Target", "Attempts", "NextAttemptAt", "LastError" FROM identity."OutboxMessages" ORDER BY "CreatedAt";`. Retry a failed row: `UPDATE identity."OutboxMessages" SET "FailedAt" = NULL, "Attempts" = 0, "NextAttemptAt" = now() WHERE "Id" = '...';`.

## Running it locally

PostgreSQL with the database `platform` (core-api's `docker-compose.yml`, or the dev server's database). Then from `identity-api/`: `dotnet ef database update --context PostgresDbContext` and `dotnet run` (port 5060). Core-api (5054) and email-api (5070) receive the outbox; until they run, messages wait and retry. The gateway (5100) routes `/api/identity/...` once core-api has seeded the `identity` project.

## Verified

On the dev server (2026-11-05, commit 3252626, through Caddy and the gateway at `https://codeclub.info.uvt.ro/dev`), a scripted run of 83 checks: 79 passed; the other 4 were test mistakes, not bugs (the outbox still retrying rows from a broken earlier deploy, a wrong field name, the gateway's 30-second access cache, and the per-IP auth rate limit kicking in). A re-check confirmed a grant takes effect after the cache time. One real email was sent over SMTP. Covered here: register (409 on a taken name), login blocked until confirmed, confirm (single use), login, the refresh cookie (`/dev/api/identity/auth`, HttpOnly, Secure, Strict), refresh rotation and reuse detection (the family is revoked), change password, forgot/reset (204 for an unknown email), the seeded admin, no role in the edge token, project tokens for core and mock, and the outbox: with core-api and email-api stopped, registration still succeeds, the messages retry with backoff and are delivered (profile created, email sent once) after they come back. `/internal/...` and `/.well-known/...` are not reachable from outside.

## Tests

`Identity.Tests/` (xunit, `dotnet test Identity.Tests`, no Docker or database): real services on hand-written in-memory repositories (`Support/Fakes.cs`, `Support/IdentityFixture.cs`) and a fake clock. The dispatcher tests start the real `OutboxDispatcher`, which delivers one batch before it waits for its timer. Run by `.github/workflows/ci.yml`.
