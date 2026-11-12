# CLAUDE.md

Instructions for `core/core-api/`. The root [CLAUDE.md](../../CLAUDE.md) also applies.

## Overview

The core API: the platform's harness data. User profiles (view, edit), projects (add, update), project roles, who has which role in which project, and the default role a project gives new users when they confirm their email. ASP.NET Core Web API on .NET 10, using controllers (not minimal APIs). Project: `CoreApi`.

Accounts, passwords and tokens are identity-api's job; email is email-api's. Behind the gateway core is the system project `core` (Exchange): it receives project tokens with audience `core`, validated by platform-kit, and the token's role is the user's core role (`User` or `Admin`, where `Admin` means platform admin). Identity tells core about new and confirmed accounts through `/internal/users`.

## Commands

Run from `core/core-api/`:

- Build: `dotnet build`
- Test: `dotnet test CoreApi.Tests` (in-memory fakes of the repositories: no Docker or database needed)
- Run: `dotnet run` (default URL in `Properties/launchSettings.json`)
- Add a migration: `dotnet ef migrations add <Name> --output-dir Data/Postgres/Migrations`
- Apply migrations: `dotnet ef database update`
- Start a local database (optional, the dev server has one): `docker compose up -d` (add `--profile tools` for pgAdmin at http://localhost:5050); stop with `docker compose down`. `docker compose down -v` deletes the data, so only run it when asked.

## Rules

- Use controllers in `Controllers/`; do not add minimal API endpoints.
- Ask before adding NuGet dependencies.
- Own tables in schema `core` only (`PostgresDbContext`, migrations history in that schema); identity and the gateway read some of them, so renaming a column of `Projects`, `ProjectRoles` or `ProjectMembers` must be coordinated with `core/identity-api/Data/CoreRead` and `core/gateway/Data`.
- Several data stores are used: PostgreSQL (relational, EF Core code-first), MongoDB (documents) and Neo4j (graph). Keep each store's code in its own folder under `Data/` and name its context/client after the store (`PostgresDbContext`). Connection strings: `PostgresConnection`, `MongoConnection`, `Neo4jConnection`.
- PostgreSQL: relational entities in `Entities/`, one `IEntityTypeConfiguration` per entity in `Data/Postgres/Configurations/`, migrations in `Data/Postgres/Migrations/`. Never edit an applied migration; add a new one.
- No passwords or tokens are handled here; keep it that way (they belong to identity-api).
- Endpoints require authentication by default; anonymous ones must say `[AllowAnonymous]`. Check roles with `[Authorize(Roles = CoreRoles.Admin)]`. Internal endpoints live under `/internal/...` with `[Authorize(AuthenticationSchemes = InternalAuth.SchemeName)]` and must be idempotent.
- Configuration lives in `appsettings*.json` and can be overridden by environment variables (`Section__Key`). Committed secrets must be clearly marked placeholders (`CHANGE-ME...`); real values come from the environment. For local development real values go in the git-ignored `appsettings.Development.local.json`, which Program.cs loads in Development only.

## Architecture

N-layer (Controller, Service, Repository) in this single project; details in [docs/architecture.md](docs/architecture.md). Rules:

- Controllers are thin: call a service, return `ToActionResult(result)` from `ApiControllerBase` (platform-kit's `ResultControllerBase`). No business logic, no try/catch (platform-kit's exception handler deals with unexpected errors), no entities.
- Services live in a folder per feature under `Services/` (`Users/`, `Projects/`, `ProjectRoles/`, `ProjectMembers/`, `Seeding/`), with the interface next to its implementation and the feature's errors (`XxxErrors`) in the same folder. Put a new service in its feature's folder, or create a new feature folder with namespace `CoreApi.Services.<Feature>`.
- Services hold all business logic, validation of business rules and error handling, and map between models and entities. They return `Result` / `Result<T>` for expected failures and throw only for unexpected ones. Entities never leave the service layer.
- Repositories use only entities, are behind interfaces, and never call `SaveChanges`; services commit through `IUnitOfWork`. Do not expose `IQueryable`. `GetAll` is always paged.
- DTOs go in `Models/Requests` and `Models/Responses` (not `Dtos`); hand-written mapping in `Mappings/`.
- All I/O is async with a `CancellationToken`; keep pure code synchronous.

## Tests

`CoreApi.Tests/` is its own project (xunit, fakes written by hand, no mocking library) inside this folder; the API project excludes it and the Docker context leaves it out. A change to behaviour comes with a test. `.github/workflows/ci.yml` runs it on every pull request and before every deployment, so a failing test blocks a deploy. Tests that need a real database are not built yet (see the backlog).

## Documentation

Keep `docs/` up to date at the end of every step (see [docs/README.md](docs/README.md)):

- `implementation.md`: what exists now and how to work with it
- `architecture.md`: the N-layer rules and folder map
- `decisions.md`: decisions taken, with the reason
- `backlog.md`: open questions, future work, things to revisit
- `changelog.md`: what was done in each step

Read `docs/` at the start of a session to see the current state.
