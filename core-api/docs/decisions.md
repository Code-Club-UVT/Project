# Decisions

| Decision | Reason |
|---|---|
| Controllers, not minimal APIs | Project requirement |
| PostgreSQL + EF Core code-first | Project requirement |
| Multiple data stores: PostgreSQL (relational), MongoDB (documents), Neo4j (graph) | Each store fits a different kind of data |
| Data code is split per store (`Data/Postgres/`, later `Data/Mongo/`, `Data/Neo4j/`); the context is named `PostgresDbContext`; connection strings are `PostgresConnection`, `MongoConnection`, `Neo4jConnection` | `DbContext` is relational-only, so a generic `AppDbContext` was misleading; naming by store makes it clear which database code touches |
| N-layer architecture (Controller, Service, Repository) in a single project with folders | Clear responsibilities; the interface boundaries allow splitting into projects later |
| Generic `IRepository<TEntity, TKey>` with specific repositories on top; interfaces for every repository | Multiple data stores and mockable tests; queries beyond the basics live in specific repositories |
| `GetAll` is always paged (size capped at 100) and ordered by primary key | An unbounded `GetAll` is dangerous on real tables; stable ordering is needed for paging |
| Repositories never call `SaveChanges`; the service commits through `IUnitOfWork` | Registration writes several rows that must commit together |
| No `IQueryable` outside the repositories | Keeps EF out of the service layer, needed for multiple stores |
| Services return `Result<T>` for expected failures, exceptions only for unexpected ones | Explicit control flow, no exceptions for normal outcomes |
| One `GlobalExceptionHandler` (500 ProblemDetails) instead of try/catch in every controller | Same behavior written once; controllers stay thin |
| HTTP status follows the error type (400/401/403/404/409), errors are ProblemDetails with a `code` field | The SPA can tell user mistakes from server failures |
| Success returns 200 (with a value) or 204 (without) | Keeps the "200 on OK" convention |
| Hand-written mapping, DataAnnotations validation, no extra packages | No dependency (AutoMapper is commercial); enough at this size |
| DTOs live in `Models/` (`Requests/`, `Responses/`), not `Dtos/` | Project naming preference |
| JWT access token signed with ES256 (ECDSA P-256); the public key is published as a JWKS (replaced HS256) | Many APIs now validate tokens. With a shared secret any of them could forge tokens for the others or for the gateway; with ES256 only core-api can sign and everyone else only gets the public key. Still one key and one signer |
| Private key and first-admin credentials come from configuration (environment variables when deployed); in Development an empty key generates a temporary one | Needed so a Docker container can be configured; startup fails outside Development without a key. A committed key would be a secret in git |
| Admin is seeded from `Seed:Admin` settings; no seeding if the settings are missing | Simple first-admin bootstrap that works in containers too |
| Register reports a taken username or email (409); resend-confirmation, forgot-password and login do not reveal whether an account exists | Register needs the feedback to be usable; the others must not leak accounts |
| "Email not confirmed" is returned only after the password is verified | Does not reveal which accounts exist |
| Dummy password hash check for unknown users on login | Same response time for unknown users and wrong passwords |
| Endpoints require authentication by default (fallback policy); anonymous ones opt out with `[AllowAnonymous]` | A forgotten attribute cannot expose an endpoint |
| Rate limiting and CORS moved from core-api to the gateway (replaced the built-in limiter at 10 requests per minute per IP on the auth endpoints) | The gateway is the single entry point, so limits and CORS live in one place; core-api must then only be reachable through it |
| Password rules: 10 to 128 characters, no forced complexity; usernames 3 to 50 characters of letters, digits, `_`, `.`, `-` | Length matters more than composition rules |
| `AuthService` (login, refresh, logout) and `AccountService` (register, confirm, resend, forgot, reset, change password) are separate services | Keeps each service's dependencies manageable |
| Only `Microsoft.AspNetCore.Authentication.JwtBearer` was added for auth | `Microsoft.Extensions.Identity.Core` (`PasswordHasher`) already ships with the ASP.NET Core framework |
| Swagger UI gets a Bearer scheme through an OpenAPI document transformer | Lets the Authorize button work for manual testing |
| `LoggingEmailSender` is the only email sender and is registered only in Development | No SMTP server yet; the app fails at startup elsewhere rather than silently not sending mail |
| `Services/` is grouped by feature (Auth, Users, Tokens, Email, Seeding), interface next to implementation; errors live next to the feature that owns them | One feature in one place; repositories use `Abstractions/` because they have several implementations per store, services have one |
| `docker-compose.yml` lives in `core-api/`, with `.env.example` and `docker/` for supporting files | Infrastructure for this API, kept inside the project (the repo root also holds the thesis) |
| PostgreSQL 17 pinned by major version, data in a named volume, port published on `127.0.0.1` only | Data survives container restarts; the database is not reachable from the network |
| pgAdmin 9 runs only with the `tools` compose profile, in desktop mode (no login), with the database pre-registered | Handy locally, but a database admin UI must not run by default on a server |
| Guid primary keys | Safe to expose in tokens and URLs |
| Swagger UI via `Swashbuckle.AspNetCore.SwaggerUI` over the built-in OpenAPI document | The .NET 10 template has no UI; this adds only the UI and keeps the built-in generator |
| `User` holds credentials only; other details live in `UserProfile` (1:1) | Keeps the login path table small; profile fields change without touching auth |
| Role is one enum column on `User` (`User`, `Admin`), stored as text | Simple needs; text keeps the data valid if the enum is reordered |
| `citext` for `Username` and `Email` | Case-insensitive uniqueness with plain unique indexes |
| Login accepts username or email in one field; usernames cannot contain `@` (check constraint) | Makes the lookup unambiguous |
| JWT access token + rotating refresh token | Short-lived stateless access, revocable long-lived refresh |
| Refresh and `UserToken` values stored as SHA-256 hashes | A DB leak must not give usable tokens; a fast hash is fine for high-entropy random values |
| `FamilyId` on refresh tokens | Reuse detection revokes only the affected session family |
| `FamilyStartedAt` on refresh tokens | Absolute session cap, rotation cannot extend a session forever |
| Refresh token in an `HttpOnly` `SameSite=Strict` cookie; access token in memory | The SPA's JavaScript (and XSS) cannot read the refresh token |
| No IP or user agent stored on refresh tokens | Not needed yet; avoids storing personal data |
| `UserToken` table for email confirmation and password reset | One mechanism, hashed and single-use |
| Login blocked until the email is confirmed | Safer default |
| Email sending behind an interface, with a simple dev implementation that logs the links | No SMTP server needed yet |
| MailKit for SMTP, selected by `Smtp:Enabled` | `System.Net.Mail.SmtpClient` is marked obsolete; MailKit is maintained and supports STARTTLS properly. The same code works in Docker, configured through `Smtp__*` environment variables |
| Emails are sent inline, failures are logged and cause a 500 | Simplest for now; a background queue with retries is in the backlog |
| Email HTML as nested tables with inline styles (no modern CSS) | Renders in all clients, Outlook included; flexbox, grid and external CSS are unreliable in email |
| Templates are a pure class in this project for now | Moved to a shared project or a service when a second API needs them (see backlog) |
| Every API lives behind the gateway at `/api/<slug>`; core-api is the system project `core` | One public origin (no CORS between APIs, one TLS endpoint, one place for limits, logs and auth) |
| Two token kinds: the edge token (audience `gateway`, global role) and project tokens (audience = slug, project role) | Audiences stop a token being replayed against another API; a project API never sees the global role |
| Core-api mints all tokens, including project tokens, through an internal endpoint called by the gateway (`X-Internal-Key`); the gateway only reads the project tables | Core-api owns users and access, so there is one signer and one rule set. The gateway caches the minted token, so the call is rare |
| Core is `PassThrough` (token forwarded unchanged, requests without a token forwarded anonymously); other projects are `Exchange`. Only system projects can be `PassThrough` | Core-api already validates its own edge token and has its own `[AllowAnonymous]` endpoints (login, register). A pass-through project on an admin-chosen URL would leak edge tokens |
| Authorization is by role only; no permission columns | Out of scope for now; the project role name is the `role` claim |
| One role per user per project (PK `(UserId, ProjectId)`); a composite FK ties the role to the same project | Simple model; the database prevents a role from another project |
| Public path is derived from the slug; `BaseUrl` may include a path (core: `.../api`) | No path column or strip flag; core's `/api/core/auth` becomes `/api/auth` upstream |
| Refresh cookie path is configuration (`/api/core/auth`) | The browser sees the gateway path, not core-api's own path |
| `ResultControllerBase` (no route) under `ApiControllerBase` (adds `api/[controller]`) | Internal and well-known controllers reuse the Result mapping with their own routes |
| One `ProjectsController` holds the project, role and member endpoints | Nested routes (`/api/projects/{id}/roles`) without relying on how route attributes combine across base classes |
| Project URLs are validated (http/https, no credentials, no loopback/link-local/metadata outside Development); private ranges allowed | The gateway calls these URLs, so an admin account must not be able to aim it at the host or a metadata service. Containers need private ranges |
| `/health` checks PostgreSQL; `HealthUrl` per project for the gateway's probes | Cheap to add now; a monitoring dashboard is deferred (backlog) |
| `UseHttpsRedirection` removed from core-api | Its only caller is the gateway over plain http; with an https port configured (for example the `https` launch profile) the redirect made JWKS fetches, token exchange and proxied calls answer 307 (found by code review). TLS belongs in front of the gateway |
