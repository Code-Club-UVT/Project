# Changelog

## Repository restructure
- Moved to `core/core-api/` (`core/` for the platform, `common/` for the shared libraries, one top-level folder per app). Paths in the project files, Dockerfile and docs follow; no behaviour change.

## Tests
- New `CoreApi.Tests` (139 tests, in-memory fakes of the repositories): `ProjectUrlRules` (loopback, link-local, metadata, mapped addresses, credentials), `ProjectService` (slugs, reserved names, URLs, system projects, default role, delete order), `ProjectRoleService`, `ProjectMemberService` (grant, change, revoke, my projects without URLs), `UserService`, `UserProvisioningService` (idempotent upsert and confirmation, the seed admin), `UserTabService` and `UserTabRules`, and the authorization attributes of every controller

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

## Step 8: SMTP email sender
- Added the `MailKit` package, `SmtpOptions` (`Smtp` section) and `SmtpEmailSender` (confirmation and reset emails, text + HTML)
- `AddServices` now takes the configuration: `Smtp:Enabled=true` registers the SMTP sender; otherwise the logging sender in Development; other environments fail at startup
- SMTP options are validated at startup when enabled (the password cannot be the placeholder)
- Placeholders added to `appsettings.json` and `appsettings.Development.json` (disabled by default)
- Build is clean
- Verified against a real SMTP account (Gmail app password): registration sent the confirmation email and forgot-password sent the reset email; both arrived in the inbox. The test user was deleted afterwards
- Not tested: SMTP failure (wrong password), and opening the emailed links in the SPA (no frontend yet)

## Step 9: styled email templates
- Added `EmailTemplates` (confirmation and password reset): table-based HTML with inline styles, a bulletproof button, the link repeated as text, and a plain-text version
- `SmtpEmailSender` now only builds and sends the message from the template output
- Build is clean; both emails were sent through SMTP to the test inbox and the test user was deleted. Not verified by me: how they look in the inbox

## Step 10: projects, project access and ES256 tokens (preparation for the gateway)
- Tokens: HS256 shared secret replaced by ES256. `SigningKeyProvider` loads `Jwt:PrivateKeyPem` (temporary key in Development when empty), `GET /.well-known/jwks.json` publishes the public key. Edge token audience is now `gateway`; `TokenService.CreateProjectToken` makes project tokens (audience = slug, `role` = project role, 5 minutes)
- New entities `Project`, `ProjectRole`, `ProjectMember`, enum `ProjectTokenMode`, configurations, three repositories, migration `AddProjects` (not applied to a database yet)
- New services `ProjectService`, `ProjectRoleService`, `ProjectMemberService`, `ProjectTokenService`, `ProjectUrlRules`; `ProjectSeeder` creates the `core` project from `Seed:CoreProject`
- New endpoints: `ProjectsController` (projects, roles, members; Admin), `GET /api/users/me/projects`, `POST /internal/project-tokens` (gateway only, `X-Internal-Key`), `/health`
- Rate limiting and CORS removed from core-api (the gateway will own them); refresh cookie path is now `RefreshToken:CookiePath` (`/api/core/auth`)
- `ApiControllerBase` split into `ResultControllerBase` and `ApiControllerBase`
- No new NuGet packages
- Verified without a database: build, JWKS output, token signatures checked with Node `crypto`, audience separation, internal key scheme, startup guards, migration SQL

## Step 11: step 10 verified against PostgreSQL
- Applied `AddProjects` to the Docker database and ran a scripted check of the project, role, member and internal token endpoints, the seeder, the cascades and the composite foreign key: 56 checks, all passed (details in [implementation.md](implementation.md)). No code changes were needed
- The `gateway-auth` library was built and verified against this API (see `gateway-auth/docs/`)

## Step 12: the gateway
- The gateway (`gateway/`) was built and verified against this API: login, refresh cookie path, pass-through of the edge token, and token exchange through `/internal/project-tokens` all work through it (see `gateway/docs/`). No core-api code changes

## Step 13: fixes from the code review
- Removed `UseHttpsRedirection` (see [decisions.md](decisions.md)); verified that a plain-http request is answered with 200, not redirected, while an https endpoint is also configured
- Other fixes of this review are in `gateway/` and `gateway-auth/`. Open points from the review are in [backlog.md](backlog.md)

## Deployment support

- Added `Dockerfile` (targets `api` and `migrate`: the EF migration bundle for the deployment job) and `Dockerfile.dockerignore`.
- In the deployment the whole configuration (secrets included) is a JSON file mounted as `appsettings.Production.json`; no code change needed. Setup documented in `~/codeclub-uvt/README.md` on the server.
- `appsettings.Development.json` no longer holds the SMTP password (placeholder); the real one is in the git-ignored `appsettings.Development.local.json`, loaded in Development only.

## Step 14: service split
- Moved out: accounts, login, refresh, password flows, token signing, JWKS and project-token minting to `identity-api/`; email sending and templates to `email-api/`; `Result`, `ResultControllerBase`, the exception handler and the internal key scheme to `platform-kit/` (was `gateway-auth`). `auth-design.md` moved to identity-api/docs
- `User` is gone; `UserProfile` (keyed by the account id from identity) holds the name and a read copy of username and email. The global `UserRole` is gone: platform admin is the `Admin` role on the `core` project
- Core is now an `Exchange` project: it validates project tokens (`aud=core`) with platform-kit. Change password moved to identity (`/api/identity/account/change-password`)
- New: `Projects.DefaultRoleId` (set with `PUT /api/projects/{id}`), granted on email confirmation; `/internal/users` and `/internal/users/{id}/confirmed` for identity's outbox; CHECK that only a system project is `PassThrough`; the seeder creates `core` (roles `User`, default, and `Admin`) and `identity`; `Seed:AdminEmail` makes the first admin
- Fresh start: own schema `core`, old migrations replaced by a new `InitialCreate`; local database renamed `coreapi` to `platform`
- Dropped MailKit and the JwtBearer package reference (it comes with platform-kit); `CoreApi.http` replaced by `identity-api/IdentityApi.http` and `gateway/Gateway.http`
- Verified on the dev server, which now runs the split services from `stack-v2/` (see implementation.md and the server README)

## Saved tabs
- New table `core.UserTabs` (migration `UserTabs`): the apps a user has open in the harness, in order, with the last path inside each. The foreign key to `ProjectMembers` cascades, so losing access or deleting the project removes the tab
- New endpoints `GET` and `PUT /api/users/me/tabs` (`UserTabService`, whole-list replace, limits and path rules in `UserTabRules`); the API uses project ids
- The service and the path rules were checked on dev; `CoreApi.Tests` (added later, see below) now covers them

## Harness shell: project URLs
- `Project.BaseUrl` renamed `BackendUrl` (nullable); added nullable `FrontendUrl`. Migration `ProjectBackendFrontendUrls` (rename, not drop). Seed settings are now `Seed:*Project:BackendUrl`
- Create/update requests and `ProjectResponse` carry both URLs; `HealthUrl` needs a `BackendUrl`; the URL rules apply to each URL given
- `MyProjectResponse` gains `HasFrontend` and `HasBackend`
- System projects keep their URLs locked, including `FrontendUrl`

