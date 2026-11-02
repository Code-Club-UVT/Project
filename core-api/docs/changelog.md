# Changelog

## Step 1: project setup
- Created `core-api` (.NET 10 Web API with controllers), project `CoreApi`
- Removed the sample weather controller and model
- Added Swagger UI at `/swagger`; `http` and `https` launch profiles open it
- Added `.gitignore` (`dotnet new gitignore`)

## Step 2: database entities
- Added EF Core with PostgreSQL, `dotnet-ef` local tool
- Added entities `User`, `UserProfile`, `RefreshToken`, `UserToken`, enums `UserRole`, `UserTokenType`
- Added `AppDbContext`, per-entity configurations, `DefaultConnection` placeholder connection string
- Created the `InitialCreate` migration (not applied to a database yet)
- Added this `docs/` folder

## Step 3: per-store data layout
- Renamed `AppDbContext` to `PostgresDbContext`, moved it with the configurations and migrations to `Data/Postgres/`
- Renamed the connection string `DefaultConnection` to `PostgresConnection`
- Regenerated the (unapplied) `InitialCreate` migration; the generated SQL is identical

## Step 4: architecture foundation
- Added `Result`, `Result<T>`, `Error`, `ErrorType`, `PagedResult<T>`
- Added `IRepository<,>`, `IUnitOfWork` and the Postgres implementations
- Added `GlobalExceptionHandler` (500 ProblemDetails) and `ApiControllerBase` (Result to HTTP mapping)
- Added DI extension methods; `Program.cs` now uses them
- Verified with a temporary controller (all status codes, the 500 handler, DI resolution of the generic repository); the controller was removed
- Added `architecture.md`

## Step 5: authentication and authorization
- Added the `Microsoft.AspNetCore.Authentication.JwtBearer` package
- Options classes (`Jwt`, `RefreshToken`, `Auth`, `Frontend`, `Seed`) validated at startup; settings in `appsettings.json`
- Specific repositories: users, refresh tokens, user tokens
- Models (requests and responses), `UserMappings`
- `TokenService` (JWT, random tokens, SHA-256 hashing), `AuthService`, `AccountService`, `UserService`, `IEmailSender` with `LoggingEmailSender`, `AdminSeeder`
- `AuthController` (register, confirm-email, resend-confirmation, login, refresh, logout, forgot-password, reset-password) and `UsersController` (me, update me, change password, admin list)
- JWT bearer authentication, fallback "authenticated" policy, role-based authorization, rate limiting, CORS for the SPA, Swagger Bearer scheme
- Verified without a database with a temporary controller (removed afterwards): token validation, 401/403, validation messages, cookie attributes, CORS, rate limit, startup guards
- Not verified: everything that needs the database

## Step 6: services folder reorganised
- `Services/` is now grouped by feature: `Auth/`, `Users/`, `Tokens/`, `Email/`, `Seeding/`, with namespaces `CoreApi.Services.<Feature>`
- `UserNotFound` moved from `AuthErrors` to the new `UserErrors.NotFound`
- No behavior change; build is clean

## Step 7: Docker development environment and end-to-end test
- Added `docker-compose.yml` (PostgreSQL 17 with a named volume, healthcheck, localhost-only port), `.env.example`, `.env` in `.gitignore`
- Added pgAdmin 9 under the `tools` profile with `docker/pgadmin/servers.json`
- Checked: container healthy, `citext` available, data survives `down`/`up`
- Applied the `InitialCreate` migration to the Docker database
- Ran a scripted end-to-end test of all auth flows against the real database: 53 checks, all pass. Six checks first failed because of an argument mistake in the test script (the calls never ran); running them directly confirmed change password works (400, 400, 204, 401, 401, 200)
- The test left users `admin`, `john` and `mary` in the development database
