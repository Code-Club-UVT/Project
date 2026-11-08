# Current implementation

Since the service split, core-api holds the platform's "harness" data: user profiles, projects, project roles, memberships and default roles. Accounts and tokens are in identity-api, email in email-api (see their docs; the history before the split is in [changelog.md](changelog.md)).

## Stack

- .NET 10, ASP.NET Core Web API with controllers
- EF Core 10 (code-first migrations) with PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- platform-kit: project-token validation (audience `core`), internal key scheme, results/ProblemDetails, request context, health
- Planned: MongoDB (document data) and Neo4j (graph data), each with its own driver and its own folder under `Data/`
- OpenAPI at `/openapi/v1.json`, Swagger UI at `/swagger` (Development only); `dotnet-ef` is a local tool

## Project layout

```
core-api/
  Common/             PostgresHealthCheck, BearerSecuritySchemeTransformer
  Controllers/        ApiControllerBase (api/[controller] on platform-kit's ResultControllerBase), UsersController,
                      ProjectsController, InternalUsersController (/internal/users, identity only)
  Data/Postgres/      PostgresDbContext (schema core), DesignTimeDbContextFactory, Configurations/, Migrations/, Repositories/
  Entities/           UserProfile, Project, ProjectRole, ProjectMember
  Enums/              ProjectTokenMode
  Extensions/         DI (Postgres, repositories, services, auth), ClaimsPrincipal helper
  Mappings/           UserMappings, ProjectMappings
  Models/             Requests/, Responses/
  Options/            SeedOptions
  Repositories/Abstractions/   IRepository<,>, IUnitOfWork, user profiles, projects, project roles, project members
  Services/           Users/ (UserService, UserProvisioningService), Projects/ (ProjectService, ProjectUrlRules, CoreRoles),
                      ProjectRoles/, ProjectMembers/, Seeding/ (ProjectSeeder)
  docker-compose.yml  local PostgreSQL (database platform) and pgAdmin
  docs/
```

See [architecture.md](architecture.md) for the layer rules.

## Database model (schema `core`)

```
UserProfile 1 -- * ProjectMember * -- 1 Project
ProjectRole 1 -- * ProjectMember
Project 1 -- * ProjectRole
Project * -- 0..1 ProjectRole (DefaultRoleId)
```

### UserProfile
`UserId` (PK, the account id from identity, not generated here), `Username` (50) and `Email` (256): read copy kept in step by identity (indexed by username, not unique: identity owns uniqueness), `FirstName?`, `LastName?` (100), `CreatedAt`, `UpdatedAt`. Created by identity's `user-registered` event.

### Project
An API behind the gateway, reachable at `/api/{Slug}`.

| Column | Notes |
|---|---|
| `Id` | uuid PK |
| `Slug` | varchar(50), unique; check `^[a-z0-9]+(-[a-z0-9]+)*$`. Part of the public path and the token audience, so it never changes |
| `Name`, `Description?` | display |
| `BackendUrl?` | where the gateway forwards API calls (`/api/<slug>`); may include a path (core is `http://core-api:8080/api`, so `/api/core/users/me` becomes `/api/users/me`). Null = no backend, so no API route |
| `FrontendUrl?` | internal address of the project's static frontend; the gateway serves it at `/apps/<slug>/` and the harness shows it in a tab. Null = no frontend. Never sent to browsers. A project may have a backend, a frontend, both or neither |
| `HealthUrl?` | full URL the gateway probes (only valid together with a `BackendUrl`) |
| `TokenMode` | `Exchange` (gateway swaps the edge token for a project token) or `PassThrough` (token forwarded as is). Check: only a system project can be `PassThrough` (identity) |
| `IsActive` | inactive projects get no route, no token and no default-role grants |
| `IsSystem` | created by the app (`core`, `identity`): URLs and active state cannot be edited, cannot be deleted |
| `DefaultRoleId?` | FK to `ProjectRoles`, `ON DELETE SET NULL`. The role new users get when their email is confirmed; null = invite-only |
| `CreatedAt`, `UpdatedAt` | UTC |

### ProjectRole
`Id`, `ProjectId` (FK, cascade), `Name` (citext(50), unique per project), `Description?`, timestamps. The name becomes the `role` claim of the project token. Unique key `(ProjectId, Id)` so a membership can reference it with its project.

### ProjectMember
PK `(UserId, ProjectId)`: one role per user per project. `ProjectRoleId`, `GrantedByUserId?` (FK to `UserProfiles`, set null; null for default-role grants), `GrantedAt`, `UpdatedAt`. The FK `(ProjectId, ProjectRoleId)` to `ProjectRoles(ProjectId, Id)` guarantees the role belongs to the same project. Deleting a profile cascades to its memberships. A role cannot be deleted while a member uses it.

## Endpoints

All public endpoints are reached through the gateway at `/api/core/...` with a **project token** (`aud=core`). The role in that token is the user's role in the `core` project: `User` or `Admin`.

| Endpoint | Access | Result |
|---|---|---|
| `GET /api/users/me` | any core member | the profile; 404 until identity's event arrived |
| `PUT /api/users/me` | any core member | updates first and last name |
| `GET /api/users/me/projects` | any core member | the user's active projects and role in each, with `hasFrontend` and `hasBackend` flags (never the URLs); the harness shows a tab for each project with a frontend |
| `GET /api/users?page=&pageSize=` | Admin | paged profiles (username order) |
| `/api/projects` `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` | Admin | projects; `PUT` takes `defaultRoleId` (a role of this project, or null) |
| `/api/projects/{id}/roles` `GET`, `POST`, `PUT {roleId}`, `DELETE {roleId}` | Admin | roles |
| `/api/projects/{id}/members` `GET`, `POST {userId, roleId}`, `PUT {userId}`, `DELETE {userId}` | Admin | memberships |
| `POST /internal/users` | identity (internal key) | create the profile, or refresh username and email; idempotent |
| `POST /internal/users/{id}/confirmed` | identity (internal key) | grant default roles; idempotent; 404 until the profile exists |
| `GET /health` | anonymous | PostgreSQL reachable |

Rules (`ProjectService`, `ProjectRoleService`): the slug is lowercased and cannot be `core`, `identity`, `email`, `gateway`, `internal`, `health` or `admin`; admin-created projects are always `Exchange`; `PassThrough` projects have no roles, members or default role; URLs must be http(s) without credentials, query or fragment, and outside Development not loopback, link-local or `metadata.google.internal`. The `Admin` role of the core project cannot be renamed or deleted. Deleting a project clears its default role first, then cascades to roles and members.

## Default roles

- A project opts in by setting `DefaultRoleId` (`PUT /api/projects/{id}` with `defaultRoleId`). Without it the project is invite-only.
- When identity reports `user-confirmed`, `UserProvisioningService.ConfirmedAsync` adds a membership with the default role for every **active** project that has one and where the user is not a member yet. It is not retroactive: setting a default later does not touch existing users.
- The `core` project's default role is `User` (seeded), so every confirmed user can use core. The user whose email is `Seed:AdminEmail` gets `Admin` on core instead.

## Seeding (`ProjectSeeder`, at startup)

- `core`: Exchange, system, from `Seed:CoreProject` (`BackendUrl`, `HealthUrl`); roles `User` (made the default the first time) and `Admin`.
- `identity`: PassThrough, system, from `Seed:IdentityProject`.
- A project without a base URL is skipped with a warning; URLs are updated when the configuration changes. A database failure is logged and the app still starts.

## Configuration

| Section | Keys |
|---|---|
| `ConnectionStrings:PostgresConnection` | database `platform` |
| `PlatformAuth` | `Issuer` (`identity`), `Audience` (`core`), `JwksUrl` (identity's `/.well-known/jwks.json`), `AllowInsecureJwksUrl` (true on the private network) |
| `InternalAuth:Callers` | `identity`: the key identity sends |
| `Seed` | `AdminEmail`, `CoreProject:BackendUrl`/`HealthUrl`, `IdentityProject:BackendUrl`/`HealthUrl` |

Development values (`appsettings.Development.json`): database on localhost, JWKS at `http://localhost:5060`, core at 5054, identity at 5060, `AdminEmail` = identity's seeded `admin@example.com`.

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

`docker-compose.yml` runs local infrastructure (optional; the dev server has a database too):

```
docker compose up -d                       # PostgreSQL only
docker compose --profile tools up -d       # PostgreSQL + pgAdmin
docker compose down                        # stop, keep the data
docker compose down -v                     # stop and DELETE the data (volumes)
```

PostgreSQL 17 on `127.0.0.1:5432`, database `platform`, user and password `postgres`; pgAdmin on `http://localhost:5050` with the profile `tools`. All three services (identity, core, email) migrate their own schema into this one database. Tables use capitals, so quote them: `SELECT * FROM core."Projects";`.

## Verified

On the dev server (2026-11-05, commit 3252626, through Caddy and the gateway at `https://codeclub.info.uvt.ro/dev`), a scripted run of 83 checks: 79 passed; the other 4 were test mistakes, not bugs (the outbox still retrying rows from a broken earlier deploy, a wrong field name, the gateway's 30-second access cache, and the per-IP auth rate limit kicking in). A re-check confirmed a grant takes effect after the cache time. One real email was sent over SMTP. Covered here: the seeded `core` and `identity` projects and core roles (`User` default), the profile created from identity's event (with the name), no membership before confirmation, `User` on core after it, `Admin` for `Seed:AdminEmail`, profile read and update, 403 for non-admins on admin endpoints, 401 without or with a bad token, project and role CRUD, `defaultRoleId` (400 for a foreign role), a new user getting the default role of another project, not retroactive for existing users, the protected core `Admin` role, the reserved `identity` slug, deleting a project that has a default role, and the database roles (each service limited to its schema and the tables it reads). Earlier verifications (before the split) are in the changelog steps 5 to 13.
