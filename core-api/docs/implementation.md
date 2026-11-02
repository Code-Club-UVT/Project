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
    GlobalExceptionHandler.cs
    PasswordRules.cs, BearerSecuritySchemeTransformer.cs
  Controllers/        ApiControllerBase, AuthController, UsersController
  Data/
    Postgres/
      PostgresDbContext.cs
      Configurations/   one IEntityTypeConfiguration per entity
      Migrations/       EF Core migrations
      Repositories/     PostgresRepository<,>, PostgresUnitOfWork, user / refresh token / user token repositories
    (Mongo/, Neo4j/ later)
  Entities/           User, UserProfile, RefreshToken, UserToken (relational)
  Enums/              UserRole, UserTokenType
  Extensions/         DI registration (Postgres, repositories, services, auth, rate limiting, CORS), ClaimsPrincipal helper
  Mappings/           UserMappings (entity -> UserResponse)
  Models/             Requests/, Responses/
  Options/            JwtOptions, RefreshTokenOptions, AuthOptions, FrontendOptions, SeedOptions
  Repositories/Abstractions/   IRepository<,>, IUnitOfWork, IUserRepository, IRefreshTokenRepository, IUserTokenRepository
  Services/           grouped by feature: Auth/, Users/, Tokens/, Email/, Seeding/ (see architecture.md)
  docs/
```

See [architecture.md](architecture.md) for the layer rules and [auth-design.md](auth-design.md) for the endpoints.

One folder per data store under `Data/`, and each store's context/client class is named after the store (`PostgresDbContext`). EF Core's `DbContext` only covers the relational side; MongoDB and Neo4j use their own drivers.

## Database model

```
User 1 -- 1 UserProfile
User 1 -- * RefreshToken
User 1 -- * UserToken
```

Table names are the `DbSet` names: `Users`, `UserProfiles`, `RefreshTokens`, `UserTokens`.

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

## Configuration

- Connection string: `ConnectionStrings:PostgresConnection` (future stores: `MongoConnection`, `Neo4jConnection`) in `appsettings.Development.json` (placeholder local credentials, see [backlog.md](backlog.md)).
- Auth settings (`Jwt`, `RefreshToken`, `Auth`, `Frontend`, `Seed`) are in `appsettings.json`; see the table in [auth-design.md](auth-design.md). Options are validated at startup.
- `Jwt:SigningKey` in `appsettings.json` is a `CHANGE-ME...` placeholder. It works only in Development; any other environment refuses to start until it is overridden, for example with the environment variable `Jwt__SigningKey` in the Docker container.
- `Seed:Admin` (username, email, password) in `appsettings.Development.json` creates the first admin at startup if the user does not exist. Elsewhere, set `Seed__Admin__Username`, `Seed__Admin__Email` and `Seed__Admin__Password`. If the database is not reachable the seeder logs a warning and the app still starts.
- Email: only `LoggingEmailSender` exists. It writes the confirmation and reset links to the log and is registered only in Development; other environments fail at startup until a real `IEmailSender` is added in `AddServices`.
- Frontend: `Frontend:BaseUrl` is `http://localhost:5173` (used for CORS and the emailed links).
- `RefreshToken:CookieSecure` is true by default and false in Development, so the cookie works over plain HTTP locally.

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
- The scripted run left test users (`admin`, `john`, `mary`) in the database. To start clean: `docker compose down -v` then `docker compose up -d` and `dotnet ef database update`.

## Not implemented yet

A real email sender, account lockout, queryable logs, MongoDB and Neo4j. See [backlog.md](backlog.md).
