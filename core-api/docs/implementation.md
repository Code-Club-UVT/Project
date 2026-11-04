# Current implementation

## Stack

- .NET 10, ASP.NET Core Web API with controllers
- EF Core 10 (code-first migrations) with PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`) for relational data
- Planned: MongoDB (document data) and Neo4j (graph data), each with its own driver and its own folder under `Data/`
- OpenAPI document at `/openapi/v1.json`, Swagger UI at `/swagger` (Development only)
- `dotnet-ef` is a local tool, declared in `dotnet-tools.json`

## Project layout

```
core-api/
  Common/
    Results/          Error, ErrorType, Result, Result<T>, PagedResult<T>
    GlobalExceptionHandler.cs, InternalKeyAuthenticationHandler.cs, PostgresHealthCheck.cs
    PasswordRules.cs, BearerSecuritySchemeTransformer.cs
  Controllers/        ResultControllerBase (Result to HTTP), ApiControllerBase (adds the api/[controller] route), AuthController, UsersController, ProjectsController, InternalProjectTokensController, WellKnownController (JWKS)
  Data/
    Postgres/
      PostgresDbContext.cs
      Configurations/   one IEntityTypeConfiguration per entity
      Migrations/       EF Core migrations
      Repositories/     PostgresRepository<,>, PostgresUnitOfWork, user / refresh token / user token / project / project role / project member repositories
    (Mongo/, Neo4j/ later)
  Entities/           User, UserProfile, RefreshToken, UserToken, Project, ProjectRole, ProjectMember (relational)
  Enums/              UserRole, UserTokenType, ProjectTokenMode
  Extensions/         DI registration (Postgres, repositories, services, auth), ClaimsPrincipal helper
  Mappings/           UserMappings, ProjectMappings
  Models/             Requests/, Responses/
  Options/            JwtOptions, InternalOptions, RefreshTokenOptions, AuthOptions, FrontendOptions, SeedOptions, SmtpOptions
  Repositories/Abstractions/   IRepository<,>, IUnitOfWork, and one interface per repository (users, refresh tokens, user tokens, projects, project roles, project members)
  Services/           grouped by feature: Auth/, Users/, Tokens/, Email/, Seeding/, Projects/, ProjectRoles/, ProjectMembers/ (see architecture.md)
  docs/
```

See [architecture.md](architecture.md) for the layer rules and [auth-design.md](auth-design.md) for the endpoints.

One folder per data store under `Data/`, and each store's context/client class is named after the store (`PostgresDbContext`). EF Core's `DbContext` only covers the relational side; MongoDB and Neo4j use their own drivers.

## Database model

```
User 1 -- 1 UserProfile
User 1 -- * RefreshToken
User 1 -- * UserToken
Project 1 -- * ProjectRole
Project 1 -- * ProjectMember * -- 1 User
ProjectRole 1 -- * ProjectMember
```

Table names are the `DbSet` names: `Users`, `UserProfiles`, `RefreshTokens`, `UserTokens`, `Projects`, `ProjectRoles`, `ProjectMembers`.

### User
| Column | Type | Notes |
|---|---|---|
| `Id` | uuid | PK |
| `Username` | citext(50) | unique; check constraint forbids `@` (login accepts username or email in one field) |
| `Email` | citext(256) | unique |
| `PasswordHash` | text | hash only, never the password |
| `Role` | varchar(20) | enum `UserRole` (`User`, `Admin`) stored as text, default `User` |
| `EmailConfirmed` | bool | login is blocked until true |
| `IsActive` | bool | allows disabling an account without deleting it |
| `CreatedAt`, `UpdatedAt` | timestamptz | UTC, set in code |

`citext` (Postgres extension, created by the first migration) makes `Username` and `Email` case-insensitive, so the plain unique indexes are enough.

### UserProfile
`UserId` (PK and FK to `User`, cascade delete), `FirstName?`, `LastName?` (max 100). More columns are added later via migrations.

### RefreshToken
| Column | Purpose |
|---|---|
| `Id` | PK |
| `UserId` | owner (FK, cascade delete) |
| `FamilyId` | same value for every token rotated from one login; reuse detection revokes the whole family |
| `TokenHash` | SHA-256 (hex, 64 chars) of the raw token; unique. The raw token is never stored |
| `CreatedAt` | issue time |
| `ExpiresAt` | when this token stops working |
| `FamilyStartedAt` | time of the original login, copied on rotation; enforces an absolute session cap |
| `RevokedAt` | null while active; set on logout, rotation, reuse detection, password change |
| `ReplacedByTokenId` | the token that replaced this one on rotation (self FK, set null on delete) |

Indexes: unique `TokenHash`, `UserId`, `FamilyId`.

### UserToken
Single-use tokens for email confirmation and password reset.

| Column | Purpose |
|---|---|
| `Id` | PK |
| `UserId` | owner (FK, cascade delete) |
| `Type` | enum `UserTokenType` (`EmailConfirmation`, `PasswordReset`) stored as text; stops a token of one purpose being used for the other |
| `TokenHash` | SHA-256 (hex) of the raw token; unique |
| `CreatedAt` | issue time (also usable for rate limiting) |
| `ExpiresAt` | deadline for using the link |
| `UsedAt` | null until used; makes the token single-use |

Index: unique `TokenHash`, and `(UserId, Type)`.

### Project
An API that lives behind the gateway, reachable at `/api/{Slug}` on the gateway. Core API is the system project with slug `core`.

| Column | Notes |
|---|---|
| `Id` | uuid PK |
| `Slug` | varchar(50), unique; check constraint `^[a-z0-9]+(-[a-z0-9]+)*$`. Part of the public path and the token audience, so it never changes |
| `Name`, `Description?` | display |
| `BaseUrl` | where the gateway forwards to; may include a path (core is `http://core-api:8080/api`, so `/api/core/auth/login` becomes `/api/auth/login`) |
| `HealthUrl?` | full URL the gateway probes |
| `TokenMode` | enum `ProjectTokenMode` stored as text: `Exchange` (gateway swaps the edge token for a project token) or `PassThrough` (token forwarded as is; core only) |
| `IsActive` | inactive projects get no token and no route |
| `IsSystem` | created by the app (core): URLs and active state cannot be edited and it cannot be deleted |
| `CreatedAt`, `UpdatedAt` | UTC |

### ProjectRole
A custom role of one project. `Id`, `ProjectId` (FK, cascade), `Name` (citext(50), unique per project), `Description?`, timestamps. The name becomes the `role` claim of the project token. There is also a unique key `(ProjectId, Id)` so `ProjectMember` can reference it together with the project.

### ProjectMember
A user's access to a project. PK `(UserId, ProjectId)`, so a user has **one** role per project. `ProjectRoleId`, `GrantedByUserId?` (FK to `Users`, set null), `GrantedAt`, `UpdatedAt`. The foreign key `(ProjectId, ProjectRoleId)` to `ProjectRoles(ProjectId, Id)` guarantees the role belongs to the same project (`NO ACTION`, so deleting a project can cascade to its roles and members together). Deleting a user cascades to their memberships. Roles cannot be deleted while a member uses them (checked by the service).

## Projects, tokens and the gateway (core-api side)

The gateway (see `gateway/docs/`) forwards every browser request. Core-api provides what it needs:

- **Project management** (Admin only), `/api/projects` (public path `/api/core/projects`): `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`; `{id}/roles` (`GET`, `POST`, `PUT {roleId}`, `DELETE {roleId}`); `{id}/members` (`GET`, `POST {userId, roleId}`, `PUT {userId}` to change the role, `DELETE {userId}`). `GET /api/users/me/projects` lists the current user's projects and role in each.
- **Rules** (`ProjectService`): the slug is lowercased and cannot be `core`, `gateway`, `internal`, `health` or `admin`; admin-created projects are always `Exchange`; `PassThrough` exists only for the system project, otherwise an admin could point a project at their own host and receive users' edge tokens; `PassThrough` projects have no roles or members; URLs must be http(s) without credentials, query or fragment, and outside Development not loopback, link-local (169.254.x.x) or `metadata.google.internal` (private ranges stay allowed for containers). Host names are not resolved, so DNS rebinding is not covered.
- **Core project seeding** (`ProjectSeeder`): at startup the `core` row is created, or its URLs updated, from `Seed:CoreProject:BaseUrl` and `HealthUrl`. Without a base URL nothing happens and a warning is logged.
- **Internal token endpoint**: `POST /internal/project-tokens` with `{ userId, slug }` and the header `X-Internal-Key`. It checks the user (active), the project (exists, active, `Exchange`) and the membership again, then returns `{ token, expiresAt, roleName }`. Errors: 401 bad or missing key, 404 user or project, 403 not a member. It is outside `/api`, so the gateway never forwards browser requests to it; core-api must only be reachable through the gateway (private Docker network).
- **JWKS**: `GET /.well-known/jwks.json` (anonymous, cached 5 minutes) publishes the public ES256 key. The gateway and the downstream APIs validate tokens with it.
- **Health**: `GET /health` (anonymous) checks that PostgreSQL is reachable (200 or 503).
- Rate limiting and CORS are no longer done here; the gateway does both.

## Configuration

- Connection string: `ConnectionStrings:PostgresConnection` (future stores: `MongoConnection`, `Neo4jConnection`) in `appsettings.Development.json` (placeholder local credentials, see [backlog.md](backlog.md)).
- Auth settings (`Jwt`, `Internal`, `RefreshToken`, `Auth`, `Frontend`, `Seed`) are in `appsettings.json`; see the table in [auth-design.md](auth-design.md). Options are validated at startup.
- **Signing key**: every token is signed with ES256 (ECDSA P-256). `Jwt:PrivateKeyPem` holds the private key as PEM. In Development an empty value generates a temporary key at startup (tokens stop working after a restart, a warning is logged); any other environment refuses to start without it, for example `Jwt__PrivateKeyPem` in the container (literal `\n` sequences are turned into new lines). Generate one with `openssl ecparam -name prime256v1 -genkey -noout | openssl pkcs8 -topk8 -nocrypt`. The key id (`kid`) is derived from the public key.
- `Internal:ApiKey` (shared secret for the gateway) in `appsettings.json` is a `CHANGE-ME...` placeholder. It works only in Development; elsewhere startup fails until `Internal__ApiKey` (32+ characters) is set.
- `Seed:Admin` (username, email, password) in `appsettings.Development.json` creates the first admin at startup if the user does not exist. Elsewhere, set `Seed__Admin__Username`, `Seed__Admin__Email` and `Seed__Admin__Password`. If the database is not reachable the seeder logs a warning and the app still starts.
- `Seed:CoreProject` (`BaseUrl`, `HealthUrl`) creates or updates the `core` project row at startup. Development uses `http://localhost:5054/api` and `http://localhost:5054/health`; in Docker set `Seed__CoreProject__BaseUrl` (for example `http://core-api:8080/api`) and `Seed__CoreProject__HealthUrl`.
- Email: `IEmailSender` has two implementations, chosen in `AddServices` by `Smtp:Enabled`. `SmtpEmailSender` (MailKit, STARTTLS on 587 by default, one connection per message, text + HTML body built by `EmailTemplates`) is used when enabled. Otherwise `LoggingEmailSender` writes the links to the log, in Development only; other environments fail at startup.
  - Settings (`Smtp` section, class `SmtpOptions`): `Enabled`, `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`, `FromName`, `TimeoutSeconds`. When enabled they are validated at startup and `Password` must not be the `CHANGE-ME` placeholder.
  - In Docker set them as environment variables (`Smtp__Enabled=true`, `Smtp__Host`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromAddress`, ...). The container needs outbound access to the SMTP port; no other container setup is required.
  - Local Gmail test: enable 2-Step Verification, create an App Password, then set `Smtp:Enabled=true`, `Username`, `Password` (the app password, not the account password) and `FromAddress` in `appsettings.Development.json`. That file is tracked by git, so do not commit the real password.
  - Templates: `Services/Email/EmailTemplates.cs` returns `EmailContent(Subject, Html, Text)`. The HTML is HTML 4/XHTML-era on purpose for email clients: nested tables, inline styles, `bgcolor`/`width` attributes, a table-based button, Arial, no images or external CSS, neutral blue accent. All values are HTML-encoded. The header text is `Smtp:FromName`. To add an email, add a method that calls `Build`.
  - Sending is inline (after the database save). If SMTP fails the failure is logged (without the link) and the request returns 500, even though the user or token was already saved.
- Frontend: `Frontend:BaseUrl` is `http://localhost:5173` (used for the emailed links; CORS is now the gateway's job).
- `RefreshToken:CookieSecure` is true by default and false in Development, so the cookie works over plain HTTP locally. `RefreshToken:CookiePath` is the **public** path of the auth endpoints, `/api/core/auth` (behind the gateway). Until the gateway exists, calling core-api directly means the browser will not send the cookie back unless the path is set to `/api/auth`.

## Commands

Run from `core-api/`:

```
dotnet build
dotnet run
dotnet ef migrations add <Name> --output-dir Data/Postgres/Migrations
dotnet ef database update
dotnet ef migrations script -o out.sql    # preview the SQL without a database
```

## Docker (development)

`docker-compose.yml` in `core-api/` runs the development infrastructure. Run from `core-api/`:

```
docker compose up -d                       # PostgreSQL only
docker compose --profile tools up -d       # PostgreSQL + pgAdmin
docker compose ps
docker compose down                        # stop, keep the data
docker compose down -v                     # stop and DELETE the data (volumes)
```

- **PostgreSQL 17**, container `coreapi-postgres`, `127.0.0.1:5432` (localhost only), data in the named volume `core-api_coreapi_pgdata`. Defaults (`postgres` / `postgres` / database `coreapi`) match `PostgresConnection` in `appsettings.Development.json`. Override with a `.env` file (copy `.env.example`; `.env` is gitignored).
- **pgAdmin 9**, container `coreapi-pgadmin`, `http://localhost:5050`, only started with `--profile tools`. Desktop mode: no pgAdmin login screen (safe because the port is localhost-only). The database is pre-registered from `docker/pgadmin/servers.json` as "coreapi (docker)" (host `postgres`); pgAdmin asks for the database password (`postgres`) the first time. Its settings live in the volume `core-api_coreapi_pgadmindata`. pgAdmin logs a harmless "Network unreachable" error for its own update check.
- Tables and columns were created by EF Core with capitals, so in SQL they must be quoted: `SELECT * FROM "Users";`.
- First time: `docker compose up -d`, then `dotnet ef database update`.

## Testing it

- `CoreApi.http` has a request for every endpoint. Swagger UI (`/swagger`) has an Authorize button for the access token: log in, copy `accessToken`, paste it.
- In Development the confirmation and reset links appear in the application log; the `token` query parameter goes into `confirm-email` / `reset-password`.
- Verified without a database: build, JWT validation (garbage, wrongly signed, expired and unsigned tokens are rejected), 403 for a non-admin, request validation messages, refresh-cookie attributes, CORS, rate limiting, startup guards.
- Verified against the Docker Postgres with a scripted run (53 checks): registration and case-insensitive username/email uniqueness (`citext`), login by username or email, email confirmation (single-use, wrong-type tokens rejected), refresh rotation and reuse detection (a reused token revokes the whole family), logout, forgot/reset password, change password (revokes refresh tokens), resend confirmation (older token stops working), the admin seeder, role checks, and the paged admin list. Not covered: token expiry in the database (7/30 days, 1/24 hours), and the concurrency edge cases in [backlog.md](backlog.md).
- Project step (no database, Docker was not running): build; the app starts; `/.well-known/jwks.json` returns the key; both token types verify against it with an independent implementation (Node `crypto`) and carry the expected audience and claims; core-api accepts its own edge token and rejects a project token (audience mismatch); `/internal/project-tokens` returns 401 without or with a wrong key and with a bearer token; `/health` returns 503 without a database; startup fails outside Development without `Jwt:PrivateKeyPem` and with the `Internal:ApiKey` placeholder; the migration SQL was reviewed.
- Project step against the Docker Postgres (scripted run, 56 checks, all passed): the `AddProjects` migration, the `core` project seeded from `Seed:CoreProject`, project / role / member create, update, delete and list; the 409 / 400 / 403 / 404 rules (duplicate slug, reserved slug, bad slug and URL, duplicate role name, role in use, double grant, system project edit and delete, roles on the pass-through project); `POST /internal/project-tokens` (token claims and 5 minute lifetime, 403 for a non-member, 404 for an unknown, inactive or pass-through project, 401 without or with a wrong key or with a bearer token); a role change shows in the next token; `GET /api/users/me/projects` (inactive projects hidden); admin-only checks; cascades (deleting a project removes its roles and members, deleting a user removes memberships); and the database rejects a role from another project (composite foreign key). The test users and projects were deleted afterwards. The existing `admin` account was not used (its password had been changed by the earlier auth tests).
- The scripted run left test users (`admin`, `john`, `mary`) in the database. To start clean: `docker compose down -v` then `docker compose up -d` and `dotnet ef database update`.

## Not implemented yet

Account lockout, queryable logs, MongoDB and Neo4j. See [backlog.md](backlog.md).
